﻿using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Ruminoid.Toolbox.Shell.ViewModels;

// ReSharper disable MemberCanBePrivate.Global

namespace Ruminoid.Toolbox.Shell.Core
{
    public static class Exporter
    {
        public static object ExportProjectToObject(
            ProjectViewModel project) =>
            new
            {
                version = 1,
                type = "project",
                operation = project.OperationModel.Id,
                sections =
                    project.ConfigSections
                        .Select(x =>
                            new
                            {
                                type = x.Item1.Id,
                                data = x.Item2
                            })
                        .ToArray()
            };

        public static JObject ExportProjectToJObject(
            ProjectViewModel project) =>
            JObject.FromObject(ExportProjectToObject(project));

        public static string ExportProjectToJsonString(
            ProjectViewModel project) =>
            ExportProjectToJObject(project).ToString();

        public static void ExportProjectToFile(
            ProjectViewModel project,
            string path) =>
            File.WriteAllText(path, ExportProjectToJsonString(project));
    }
}