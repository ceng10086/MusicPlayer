using System;
using System.Collections.Generic;

namespace MusicPlayer.Models
{
    public class LyricLine
    {
        public TimeSpan Time { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}
