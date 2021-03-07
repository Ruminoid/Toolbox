﻿using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ruminoid.Toolbox.Core;
using Ruminoid.Toolbox.Utils;
using Ruminoid.Toolbox.Utils.Extensions;

namespace Ruminoid.Toolbox.Composition.Roslim
{
    [Export]
    public class RoslimGenerator
    {
        #region Constructor

        public RoslimGenerator(
            ILogger<RoslimGenerator> logger)
        {
            _logger = logger;
        }

        #endregion

        #region TemplateEngine

        private readonly EngineConfig _engineConfig = new(
            new EngineEnvironmentSettings(
                new DefaultTemplateEngineHost(
                    "Ruminoid.Toolbox.Composition.Roslim.RoslimGenerator",
                    "0",
                    "en"),
                settings => settings.SettingsLoader),
            new VariableCollection());

        #endregion

        #region Core

        public Assembly Generate(string path)
        {
            try
            {
                // Parse Target
                string target = Path.GetExtension(path) switch
                {
                    { } e when e == ".py" => "python",
                    { } e when e == ".js" || e == ".mjs" => "node",
                    { } e when e == ".lua" => "lua53",
                    _ => throw new RoslimException("不支持的插件格式。")
                };

                // Parse Meta
                string metaResult = ExternalProcessRunner.Run(target, path.EscapePathStringForArg());
                JObject meta = JObject.Parse(metaResult);
                
                string assemblyName = meta["id"]!.ToObject<string>();

                List<IOperationProvider> providers = new List<IOperationProvider>
                {
                    new Replacement(TokenConfig.FromValue("target"), target.EscapeForCode(), "target", true),
                    new Replacement(TokenConfig.FromValue("script"), path.EscapeForCode(), "script", true),
                    new Replacement(TokenConfig.FromValue("RoslimOperation"), meta["class_name"]!.ToObject<string>(), "classname", true),
                    new Replacement(TokenConfig.FromValue("Ruminoid.Toolbox.Composition.Roslim.RoslimDefaultOperation"), assemblyName, "id", true),
                    new Replacement(TokenConfig.FromValue("(Roslim)"), meta["name"]!.ToObject<string>().EscapeForCode(),
                        "name", true),
                    new Replacement(TokenConfig.FromValue("(Roslim Operation)"),
                        meta["description"]!.ToObject<string>().EscapeForCode(), "description", true)
                };

                // Add Config Sections
                List<string> configSections = meta["config_sections"]!.ToObject<List<string>>();

                if (configSections is null)
                    throw new RoslimException($"{nameof(configSections)} 为空。请检查插件元信息。");

                StringBuilder builder = new StringBuilder();
                builder.Append('"');
                builder.Append(configSections[0].EscapeForCode());
                configSections.RemoveAt(0);

                foreach (string section in configSections)
                {
                    builder.Append("\",\"");
                    builder.Append(section.EscapeForCode());
                }

                builder.Append('"');

                providers.Add(
                    new Replacement(
                        TokenConfig.FromValue("\"Ruminoid.Toolbox.Composition.Roslim.RoslimConfigSection\""),
                        builder.ToString(),
                        "config_sections",
                        true));

                IProcessor processor =
                    Microsoft.TemplateEngine.Core.Util.Processor.Create(_engineConfig, providers);

                using MemoryStream resultStream = new MemoryStream();
                using StreamReader resultReader = new(resultStream);

                processor.Run(
                    typeof(RoslimGenerator).Assembly.GetManifestResourceStream("rmbox.Composition.Roslim.RoslimOperation.cs"),
                    resultStream);

                var resultCode = resultReader.ReadToEnd();

                if (string.IsNullOrEmpty(resultCode))
                    throw new RoslimException("生成代码时出现错误。");

                SyntaxTree syntaxTree =
                    CSharpSyntaxTree.ParseText(resultCode, new CSharpParseOptions(LanguageVersion.Latest));

                CSharpCompilation compilation = CSharpCompilation.Create(assemblyName)
                    .WithOptions(
                        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .AddReferences(MetadataReference.CreateFromFile(typeof(IMeta).Assembly.Location))
                    .AddSyntaxTrees(syntaxTree);

                using MemoryStream assemblyStream = new MemoryStream();

                if (!compilation.Emit(assemblyStream).Success)
                    throw new RoslimException("编译插件时出现问题。");

                return Assembly.Load(assemblyStream.GetBuffer());
            }
            catch (JsonException e)
            {
                throw new RoslimException("解析插件元信息时出现问题。", e);
            }
            catch (RoslimException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new RoslimException("加载Roslim插件时出现问题。", e);
            }
        }
        
        #endregion

        private readonly ILogger<RoslimGenerator> _logger;
    }
}