using OpenVisionLab.Logging;
using System;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    public static class AppLog
    {
        public enum LOG
        {
            NORMAL = 0,
            ABNORMAL,
            COMM,
            IO,
            Thread,
            INSP,
            MOTION,
            SEQ,
            ALARM,
            INTERLOCK,
            DEVICE,
            TEACHING,
            CONFIG,
            LOT
        }

        public static void Debug(params object[] values) => Write(LOG.NORMAL, LogCategory.Main, LogLevel.Debug, values);
        public static void Warn(params object[] values) => Write(LOG.ABNORMAL, LogCategory.Main, LogLevel.Warning, values);
        public static void Fatal(params object[] values) => Write(LOG.ABNORMAL, LogCategory.System, LogLevel.Error, values);
        public static void NORMAL(params object[] values) => Write(LOG.NORMAL, LogCategory.Main, LogLevel.Info, values);
        public static void ABNORMAL(params object[] values) => Write(LOG.ABNORMAL, LogCategory.Main, LogLevel.Error, values);
        public static void COMM(params object[] values) => Write(LOG.COMM, LogCategory.Pipeline, LogLevel.Info, values);
        public static void ALARM(params object[] values) => Write(LOG.ALARM, LogCategory.System, LogLevel.Warning, values);
        public static void MOTION(params object[] values) => Write(LOG.MOTION, LogCategory.Vision, LogLevel.Info, values);
        public static void DEVICE(params object[] values) => Write(LOG.DEVICE, LogCategory.System, LogLevel.Info, values);
        public static void IO(params object[] values) => Write(LOG.IO, LogCategory.System, LogLevel.Info, values);
        public static void LOT(params object[] values) => Write(LOG.LOT, LogCategory.Main, LogLevel.Info, values);
        public static void SEQ(params object[] values) => Write(LOG.SEQ, LogCategory.Pipeline, LogLevel.Info, values);
        public static void CONFIG(params object[] values) => Write(LOG.CONFIG, LogCategory.System, LogLevel.Info, values);
        public static void INTERLOCK(params object[] values) => Write(LOG.INTERLOCK, LogCategory.System, LogLevel.Warning, values);
        public static void INSP(params object[] values) => Write(LOG.INSP, LogCategory.Vision, LogLevel.Info, values);
        public static void TEACHING(params object[] values) => Write(LOG.TEACHING, LogCategory.UI, LogLevel.Info, values);

        public static Color GetColor(string type)
        {
            if (type.Contains("[NORMAL]")) { return Color.White; }
            if (type.Contains("[IO]")) { return Color.Lime; }
            if (type.Contains("[ABNORMAL]")) { return Color.Red; }
            if (type.Contains("[ALARM]")) { return Color.Red; }
            if (type.Contains("[COMM]")) { return Color.DeepSkyBlue; }
            if (type.Contains("[MOTION]")) { return Color.Teal; }
            if (type.Contains("[INSP]")) { return Color.Yellow; }
            if (type.Contains("[INTERLOCK]")) { return Color.Orange; }
            if (type.Contains("[SEQ]")) { return Color.DodgerBlue; }
            if (type.Contains("[DEVICE]")) { return Color.Yellow; }
            if (type.Contains("[Thread]")) { return Color.Silver; }
            if (type.Contains("Fatal")) { return Color.Blue; }
            return Color.White;
        }

        private static void Write(LOG logType, LogCategory category, LogLevel level, params object[] values)
        {
            OVLog.Write(category, level, BuildMessage(logType, values));
        }

        private static string BuildMessage(LOG logType, params object[] values)
        {
            string body = BuildBody(values);
            return $"[{logType}] {body}";
        }

        private static string BuildBody(object[] values)
        {
            if (values == null || values.Length == 0)
            {
                return string.Empty;
            }

            if (values[0] is string format && values.Length > 1)
            {
                try
                {
                    return string.Format(format, values.Skip(1).ToArray());
                }
                catch (FormatException)
                {
                    return string.Concat(values.Select(value => value?.ToString()));
                }
            }

            return string.Concat(values.Select(value => value?.ToString()));
        }
    }
}
