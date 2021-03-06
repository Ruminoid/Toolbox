﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Ruminoid.Toolbox.Formatting;
using Ruminoid.Toolbox.Services.CommandLine;
using Ruminoid.Toolbox.Utils;
using Ruminoid.Toolbox.Utils.Extensions;
using Websocket.Client;

namespace Ruminoid.Toolbox.Core
{
    public class ProcessRunner : IDisposable
    {
        public ProcessRunner(
            CommandLineService commandLineService,
            FormattingService formattingService,
            ILogger<ProcessRunner> logger)
        {
            _commandLineService = commandLineService;
            _formattingService = formattingService;
            _logger = logger;

            Observable
                .FromEventPattern<EventArgs>(AppDomain.CurrentDomain, nameof(AppDomain.CurrentDomain.ProcessExit))
                .ObserveOn(TaskPoolScheduler.Default)
                .Subscribe(OnProcessExit);

            Observable
                .FromEventPattern<ConsoleCancelEventArgs>(typeof(Console), nameof(Console.CancelKeyPress))
                .ObserveOn(TaskPoolScheduler.Default)
                .Subscribe(OnCancelKeyPress);

            int dynamicLinkPort = ((ProcessOptions) _commandLineService.Options).DynamicLinkPort;

            if (dynamicLinkPort != 0)
            {
                _websocketClient = new(new Uri($"ws://127.0.0.1:{dynamicLinkPort}"))
                {
                    ReconnectTimeout = TimeSpan.FromSeconds(30)
                };

#if DEBUG

                _websocketClient.ReconnectionHappened
                    .Subscribe(info =>
                    {
                        _logger.LogWarning($"Dynamic link reconnected because of {info.Type}");
                    });

#endif

                _websocketClient.MessageReceived
                    .Where(x => x.MessageType == WebSocketMessageType.Text)
                    .Where(x => x.Text == DynamicLinkKillCommand)
                    .Subscribe(_ => TryKillProcess());

                _websocketClient.Start();

                _pipeSubject
                    .Sample(TimeSpan.FromSeconds(0.5))
                    .ObserveOn(TaskPoolScheduler.Default)
                    .Subscribe(PipeSend);
            }
            
            _unsubscribe = _formattingService.FormatData.Subscribe(
                OnNext,
                OnError);
        }

        #region Dynamic Link

        // public const string DynamicLinkEndpointStr = "dynlnk";
        public const string DynamicLinkKillCommand = "RMDYNLNKKILL";

        private readonly Subject<string> _pipeSubject = new();

        private readonly WebsocketClient _websocketClient;

        private void PipeSend(string json)
        {
            _websocketClient.Send(json);
        }

        #endregion

        #region Runner

        /// <summary>
        /// 执行进程。
        /// </summary>
        /// <param name="target">进程目标。</param>
        /// <param name="args">进程参数。</param>
        /// <param name="formatter">格式器目标。</param>
        public void Run(string target, string args, string formatter)
        {
            Dictionary<string, object> sessionStorage = new();

            _logger.LogDebug($"Resolving target: {target}");

            string workingDirectory = StorageHelper.GetSectionFolderPath("tools");

            string targetPath = PathExtension.GetTargetPath(target);

            if (!File.Exists(targetPath))
            {
                string err = $"未能找到进程文件：{targetPath}。可能需要安装相关的插件以解决此问题。";
                _logger.LogCritical(err);
                throw new ProcessRunnerException(err);
            }

            string pFileName = ProcessExtension.GetPathExecutable();
            string pArgumentsIntl = targetPath.EscapePathStringForArg() + ' ' + args;
            string pArguments = (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "/c" : "-c") + ' ' +
                                pArgumentsIntl.EscapePathStringForArg(
                                    !RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            _currentProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    StandardErrorEncoding = Encoding.UTF8,
                    StandardInputEncoding = Encoding.UTF8,
                    StandardOutputEncoding = Encoding.UTF8,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    Arguments = pArguments,
                    FileName = pFileName,
                    WindowStyle = ProcessWindowStyle.Hidden
                },
                EnableRaisingEvents = true
            };
            
            // ReSharper disable once InvokeAsExtensionMethod
            IDisposable observable = Observable.Merge(
                    Observable.FromEventPattern<DataReceivedEventArgs>(_currentProcess, nameof(_currentProcess.ErrorDataReceived)),
                    Observable.FromEventPattern<DataReceivedEventArgs>(_currentProcess, nameof(_currentProcess.OutputDataReceived)))
                .ObserveOn(TaskPoolScheduler.Default)
                .Subscribe(
                    next =>
                    {
                        var (_, e) = next;
                        if (string.IsNullOrEmpty(e.Data)) return;

                        if (((ProcessOptions) _commandLineService.Options).LogProcessOut)
                            _logger.LogInformation($"[{formatter}]{e.Data}");
                        _formattingService.ReceiveData.OnNext((formatter, e.Data, sessionStorage));
                    });

            _logger.LogInformation($"开始解析：{targetPath} {args}");
            _logger.LogInformation($"开始运行：{pFileName} {pArguments}");

            _currentProcess.Start();
            _currentProcess.BeginErrorReadLine();
            _currentProcess.BeginOutputReadLine();
            _currentProcess.WaitForExit();

            if (_currentProcess.ExitCode != 0)
            {
                string err = $"{(formatter == "null" ? "命令运行" : formatter)} 出现错误，退出码为 {_currentProcess.ExitCode}。";
                _logger.LogCritical(err);

                _logger.LogWarning("等待清空输出缓冲……");
                Task.Delay(TimeSpan.FromSeconds(3)).Wait();
                observable.Dispose();

                throw new ProcessRunnerException(err);
            }

            observable.Dispose();

            _logger.LogInformation($"{(formatter == "null" ? "命令" : formatter)} 运行结束，程序正常退出。");
        }

        /// <summary>
        /// 执行进程。
        /// </summary>
        /// <param name="command">指令。</param>
        public void Run(TaskCommand command) =>
            Run(command.Target, command.Args, command.Formatter);

        /// <summary>
        /// 执行进程。
        /// </summary>
        /// <param name="commands">指令列表。</param>
        public void Run(List<TaskCommand> commands)
        {
            _logger.LogInformation($"开始运行 {commands.Count} 条指令。");

            commands.ForEach(Run);
        }

        #endregion

        #region Daemon

        private Process _currentProcess;

        private void OnProcessExit(EventPattern<EventArgs> e)
        {
            TryKillProcess();
        }

        private void OnCancelKeyPress(EventPattern<ConsoleCancelEventArgs> e)
        {
            e.EventArgs.Cancel = true;
            TryKillProcess();
        }

        private void TryKillProcess()
        {
            if (!IsProcessRunning()) return;
            _logger.LogWarning("正在尝试终止运行。应用可能在运行终止前被终止。");
            _currentProcess.Kill(true);
            _currentProcess.Dispose();
            var exception = new ProcessRunnerException("运行被用户终止。");
            _logger.LogCritical(exception, "运行被用户终止。");
            throw exception;
        }

        private bool IsProcessRunning() =>
            _currentProcess is not null &&
            !_currentProcess.HasExited;
        
        #endregion

        #region Subscribe

        private readonly IDisposable _unsubscribe;

        private void OnError(Exception error) => _logger.LogError(error, "进程发生了错误。");

        private void OnNext(FormattedEvent formatted)
        {
            if (!((ProcessOptions)_commandLineService.Options).HideFormattedOutput)
                _logger.LogInformation(formatted.ToString());
            _pipeSubject.OnNext(JsonConvert.SerializeObject(formatted));
        }

        #endregion

        private readonly CommandLineService _commandLineService;
        private readonly FormattingService _formattingService;
        private readonly ILogger<ProcessRunner> _logger;

        public void Dispose()
        {
            _unsubscribe?.Dispose();
        }
    }
}
