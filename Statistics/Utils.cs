using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Statistics
{
    public static class Utils
    {
        public static string FormatTime(TimeSpan span)
        {
            if ((int)span.TotalHours > 0)
                return string.Format("{0}h {1}m {2}s", (int)span.TotalHours, (int)span.Minutes, (int)span.Seconds);
            if ((int)span.TotalMinutes > 0)
                return string.Format("{0}m {1}s", (int)span.TotalMinutes, (int)span.Seconds);

            return string.Format("{0} seconds", (int)span.TotalSeconds);
        }

        public static string FormatTime(double seconds)
        {
            return FormatTime(TimeSpan.FromSeconds(seconds));
        }
    }
}
