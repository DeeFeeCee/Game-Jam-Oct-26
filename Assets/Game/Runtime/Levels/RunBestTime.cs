using System;
using System.Globalization;
using UnityEngine;

namespace GameJam.Levels
{
    public sealed class RunBestTime
    {
        public const string ProductionKey = "PIDR.BestCompletionTimeMs.v1";
        private readonly string key;

        public RunBestTime(string key) => this.key = key;

        public int Read() => Math.Max(0, PlayerPrefs.GetInt(key, 0));

        public bool TryImprove(long milliseconds)
        {
            if (milliseconds <= 0 || milliseconds > int.MaxValue) return false;
            int previous = Read();
            if (previous > 0 && milliseconds >= previous) return false;
            PlayerPrefs.SetInt(key, (int)milliseconds);
            PlayerPrefs.Save();
            return true;
        }

        public static string Format(long milliseconds)
        {
            long centiseconds = Math.Max(0L, milliseconds) / 10;
            long minutes = centiseconds / 6000;
            string clock = string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}.{2:D2}",
                minutes % 60, centiseconds / 100 % 60, centiseconds % 100);
            return minutes < 60 ? clock : (minutes / 60).ToString(CultureInfo.InvariantCulture) + ":" + clock;
        }
    }
}
