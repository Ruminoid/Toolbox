﻿using System.Collections.Generic;
using System.Text.RegularExpressions;
using Ruminoid.Toolbox.Formatting;

namespace Ruminoid.Toolbox.Plugins.FFmpeg.Formatters
{
    [Formatter("ffmpeg*")]
    public class FFmpegFormatter : IFormatter
    {
        public FormattedEvent Format(
            string target,
            string data,
            Dictionary<string, object> sessionStorage)
        {
            Regex progressRegex = new Regex(@"time=(.*) bitrate=");
            Match match = progressRegex.Match(data);
            if (string.IsNullOrWhiteSpace(match.Value)) return null;

            return new(target, 0, true, $"已编码长度：{match.Groups[1].Value}", "");
        }
    }
}
