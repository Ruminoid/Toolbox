﻿using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Ruminoid.Common2.Utils.UserTypes;
using Ruminoid.Toolbox.Core;
using Ruminoid.Toolbox.Utils.Extensions;

namespace Ruminoid.Toolbox.Plugins.FFmpeg.Operations
{
    [Operation(
        "Ruminoid.Toolbox.Plugins.FFmpeg.Operations.FFmpegPictureFlowOperation",
        "一图流",
        "创建一图流视频。",
        RateValue.ThreeStars,
        "视频创建")]
    public class FFmpegPictureFlowOperation : IOperation
    {
        public List<TaskCommand> Generate(Dictionary<string, JToken> sectionData)
        {
            #region IO 参数

            JToken ioSection =
                sectionData[ConfigSectionBase.IOConfigSectionId];

            string inputPathIntl = PathExtension.GetFullPathOrEmpty(ioSection["input"]?.ToObject<string>() ?? string.Empty);
            string inputPath = inputPathIntl.EscapePathStringForArg();
            string outputPath = PathExtension.GetFullPathOrEmpty(ioSection["output"]?.ToObject<string>() ?? string.Empty).EscapePathStringForArg();

            #endregion

            #region 一图流参数

            JToken pictureFlowSection =
                sectionData["Ruminoid.Toolbox.Plugins.Common.ConfigSections.PictureFlowConfigSection"];

            // ReSharper disable PossibleNullReferenceException

            string picturePath = PathExtension.GetFullPathOrEmpty(pictureFlowSection["picture"]?.ToObject<string>() ?? string.Empty).EscapePathStringForArg();
            int frameRate = pictureFlowSection["frame_rate"].ToObject<int>();
            double crfValue = pictureFlowSection["crf_value"].ToObject<double>();

            // ReSharper restore PossibleNullReferenceException

            #endregion

            #region 流处理模式

            JToken advancedTrackSection =
                sectionData["Ruminoid.Toolbox.Plugins.Common.ConfigSections.AdvancedTrackConfigSection"];

            string processMode = advancedTrackSection["process_mode"]?.ToObject<string>() ?? string.Empty;

            string audioProcessMode = inputPathIntl switch
            {
                { } e when Path.GetExtension(e) == ".aac" => "copy",
                _ => processMode switch
                {
                    "force_copy" => "copy",
                    _ => "aac"
                }
            };

            #endregion

            #region 自定义参数

            JToken customArgsSection =
                sectionData["Ruminoid.Toolbox.Plugins.Common.ConfigSections.CustomArgsConfigSection"];

            string customArgs = customArgsSection["args"]?.ToObject<string>();
            bool useCustomArgs = customArgsSection["use_custom_args"]?.ToObject<bool>() ?? false;

            #endregion

            return new List<TaskCommand>
            {
                new(
                    "ffmpeg",
                    $"-y -loop 1 -r {frameRate} -i {picturePath} -i {inputPath} -c:v libx264 -c:a {audioProcessMode} -crf {crfValue} -shortest {(useCustomArgs ? customArgs : DefaultArgs)} {outputPath}",
                    "ffmpeg")
            };
        }

        private const string DefaultArgs = "-tune stillimage -pix_fmt yuv420p";

        public Dictionary<string, JToken> RequiredConfigSections => new()
        {
            {
                ConfigSectionBase.IOConfigSectionId,
                JToken.FromObject(new
                {
                    output_suffix = "_picture",
                    output_extension = ".mp4"
                })
            },
            {
                "Ruminoid.Toolbox.Plugins.Common.ConfigSections.PictureFlowConfigSection",
                JObject.FromObject(new
                {
                    support_picture = true
                })
            },
            {"Ruminoid.Toolbox.Plugins.Common.ConfigSections.AdvancedTrackConfigSection", new JObject()},
            {
                "Ruminoid.Toolbox.Plugins.Common.ConfigSections.CustomArgsConfigSection",
                JObject.FromObject(new
                {
                    default_args = DefaultArgs
                })
            }
        };
    }
}
