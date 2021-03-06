﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Ruminoid.Toolbox.Composition.Roslim;
using Ruminoid.Toolbox.Core;
using Ruminoid.Toolbox.Formatting;
using Ruminoid.Toolbox.Utils;

namespace Ruminoid.Toolbox.Composition.Services
{
    public partial class PluginService
    {
        #region Initialize

        [PublicAPI]
        public readonly string[] IgnoreFormats = { ".json", ".pdb" };

        /// <summary>
        /// 收集并加载插件。
        /// </summary>
        private void Initialize()
        {
            _logger.LogDebug("Starting collecting plugins.");

            string pluginsFolderPath = StorageHelper.GetSectionFolderPath("plugins");

            string[] files = Directory.GetFiles(
                    pluginsFolderPath,
                    "*",
                    SearchOption.AllDirectories)
                .Where(x =>
                    Path.GetFileNameWithoutExtension(x).StartsWith("rmbox-plugin-") ||
                    Path.GetFileNameWithoutExtension(x).StartsWith("Ruminoid.Toolbox.Plugin."))
                .Where(x => !x.Replace(pluginsFolderPath, "")
                    .Contains($"ref{Path.DirectorySeparatorChar}"))
                .Where(x => !IgnoreFormats.Any(x.EndsWith))
                .ToArray();

            string[] dllFiles = files
                .Where(x => x.EndsWith(".dll"))
                .ToArray();

            string[] scriptFiles = files
                .Where(x => !x.EndsWith("dll"))
                .ToArray();

            _logger.LogDebug($"Collected {dllFiles.Length} plugin(s).");

            if (dllFiles.Length == 0)
            {
                _logger.LogInformation("没有发现插件。");
                return;
            }

            _logger.LogDebug("Loading plugins.");

            _logger.LogDebug("Loading DLL plugins.");
            LoadDllPlugins(dllFiles);

            _logger.LogDebug("Loading script plugins.");
            int scriptCount = LoadScriptPlugins(scriptFiles);

            _logger.LogInformation(
                $"加载了 {MetaCollection.Count + scriptCount} 个插件共 {OperationCollection.Count + ConfigSectionCollection.Count + FormatterCollection.Count} 个组件。");
        }

        #endregion

        #region Plugin Loader

        /// <summary>
        /// 加载 DLL 插件。
        /// </summary>
        /// <param name="dllFiles">DLL 插件列表。</param>
        private void LoadDllPlugins(string[] dllFiles)
        {
            foreach (string file in dllFiles)
            {
                try
                {
                    _logger.LogDebug($"Loading plugin: {file}");

                    Assembly pluginAssembly = LoadPlugin(file);
                    Type[] exportedTypes = pluginAssembly.GetExportedTypes();

                    string pluginName = pluginAssembly.FullName;

                    _logger.LogDebug($"Parsing meta in plugin: {pluginName}");

                    Type[] metaTypes = exportedTypes.Where(type =>
                        Attribute.GetCustomAttribute(type, typeof(RmboxPluginAttribute)) is not null).ToArray();

                    if (metaTypes.Length != 1)
                    {
                        string err = $"插件 {pluginName} 具有的元信息的个数 {metaTypes.Length} 不符合 1 的要求。";
                        _logger.LogError(err);
                        throw new IndexOutOfRangeException(err);
                    }

                    // ReSharper disable once UseNegatedPatternMatching
                    IMeta pluginMeta = Activator.CreateInstance(metaTypes.Single()) as IMeta;
                    if (pluginMeta is null)
                    {
                        string err = $"在加载插件 {pluginName} 的元信息时发生错误。";
                        _logger.LogError(err);
                        throw new ArgumentNullException(nameof(pluginMeta));
                    }

                    _logger.LogDebug($"Meta parsed in: {pluginMeta.Name}");

                    MetaCollection.Add((pluginMeta, pluginAssembly));

                    _logger.LogDebug($"Parsing components in plugin: {pluginMeta.Name}");

                    List<Type> operationTypes = exportedTypes.Where(type =>
                        Attribute.GetCustomAttribute(type, typeof(OperationAttribute)) is not null).ToList();

                    foreach (Type t in operationTypes.Where(type => !type.IsAssignableTo(typeof(IOperation)))
                        .ToArray())
                    {
                        _logger.LogWarning($"检测到错误导出的类型 {t.FullName}，将会忽略加载。");
                        operationTypes.Remove(t);
                    }

                    foreach (Type operationType in operationTypes)
                    {
                        OperationAttribute operationAttribute =
                            Attribute.GetCustomAttribute(operationType, typeof(OperationAttribute))
                                as OperationAttribute;

                        OperationCollection.Add((operationAttribute, pluginMeta, operationType));

                        _logger.LogDebug($"Operation {operationAttribute.Name} loaded.");
                    }

                    List<Type> configSectionTypes = exportedTypes.Where(type =>
                        Attribute.GetCustomAttribute(type, typeof(ConfigSectionAttribute)) is not null).ToList();

                    foreach (Type t in configSectionTypes.Where(type => !type.IsSubclassOf(typeof(ConfigSectionBase)))
                        .ToArray())
                    {
                        _logger.LogWarning($"检测到错误导出的类型 {t.FullName}，将会忽略加载。");
                        configSectionTypes.Remove(t);
                    }

                    foreach (Type configSectionType in configSectionTypes)
                    {
                        var configSectionAttribute =
                            Attribute.GetCustomAttribute(configSectionType, typeof(ConfigSectionAttribute))
                                as ConfigSectionAttribute;

                        ConfigSectionCollection.Add((configSectionAttribute, configSectionType));

                        _logger.LogDebug($"Config section {configSectionAttribute.Id} loaded.");
                    }

                    List<Type> formatterTypes = exportedTypes.Where(type =>
                        Attribute.GetCustomAttribute(type, typeof(FormatterAttribute)) is not null).ToList();

                    foreach (Type t in formatterTypes.Where(type => !type.IsAssignableTo(typeof(IFormatter)))
                        .ToArray())
                    {
                        _logger.LogWarning($"检测到错误导出的类型 {t.FullName}，将会忽略加载。");
                        formatterTypes.Remove(t);
                    }

                    foreach (Type formatterType in formatterTypes)
                    {
                        FormatterAttribute formatterAttribute =
                            Attribute.GetCustomAttribute(formatterType, typeof(FormatterAttribute))
                                as FormatterAttribute;

                        FormatterCollection.Add((formatterAttribute, formatterType));

                        _logger.LogDebug($"Formatter {formatterType.FullName} loaded.");
                    }

                    _logger.LogDebug($"Plugin {pluginMeta.Name} loaded.");
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, $"在加载插件 {file} 时发生错误。将跳过该插件的加载。");
                }
            }
        }

        private static Assembly LoadPlugin(string path)
        {
            PluginLoadContext loadContext = new PluginLoadContext(path);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(path)));
        }

        /// <summary>
        /// 加载 Roslim 插件。
        /// </summary>
        /// <param name="scriptFiles">Roslim 插件列表。</param>
        /// <returns>加载的插件数目。</returns>
        private int LoadScriptPlugins(string[] scriptFiles)
        {
            int count = 0;

            foreach (string file in scriptFiles)
            {
                try
                {
                    _logger.LogDebug($"Loading plugin: {file}");

                    var (meta, pluginAssembly) = _roslimGenerator.Generate(file);
                    Type exportedType = pluginAssembly.GetExportedTypes()
                        .First(type => Attribute.GetCustomAttribute(type, typeof(OperationAttribute)) is not null);

                    string pluginName = pluginAssembly.FullName;

                    _logger.LogDebug($"Parsing components in plugin: {pluginName}");

                    OperationAttribute operationAttribute =
                        Attribute.GetCustomAttribute(exportedType, typeof(OperationAttribute))
                            as OperationAttribute;

                    RoslimMeta pluginMeta = new(
                        meta["name"]!.ToObject<string>(),
                        meta["description"]!.ToObject<string>(),
                        meta["author"]!.ToObject<string>());

                    OperationCollection.Add(new(operationAttribute, pluginMeta, exportedType));

                    MetaCollection.Add((pluginMeta, pluginAssembly));

                    _logger.LogDebug($"Operation {operationAttribute.Name} loaded.");
                    _logger.LogDebug($"Plugin {pluginName} loaded.");

                    count++;
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, $"在加载插件 {file} 时发生错误。将跳过该插件的加载。");
                }
            }

            return count;
        }

        #endregion
    }
}
