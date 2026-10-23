using OpenVisionLab;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace MvcVisionSystem
{
    // A display-only projection: the bound runtime/catalog objects and commands stay unchanged.
    public sealed class RuntimeDetailTextConverter : IMultiValueConverter
    {
        private readonly Dictionary<string, OpenVisionLocalizationEntry> defaultEntries = OpenVisionLanguageService.GetDefaultEntries()
            .Where(entry => entry.Key.StartsWith("WpfModelDetails.", StringComparison.Ordinal))
            .ToDictionary(entry => entry.Key, StringComparer.Ordinal);
        private readonly Dictionary<string, Regex> templatePatterns = new Dictionary<string, Regex>(StringComparer.Ordinal);

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string value = values.Length > 0 ? values[0] as string ?? string.Empty : string.Empty;
            if (string.Equals(parameter as string, "RuntimeProfileNextAction", StringComparison.Ordinal))
            {
                const string key = "WpfModelDetails.Runtime.58";
                if (LocalizationTextRuntimeService.TryGetCachedTextPair(key, out string korean, out string english)
                    && (string.Equals(value, korean, StringComparison.Ordinal) || string.Equals(value, english, StringComparison.Ordinal)
                        || OpenVisionLanguageService.GetPreviousKoreanDefaults(key).Contains(value, StringComparer.Ordinal)))
                {
                    string translated = OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean ? korean : english;
                    return string.IsNullOrWhiteSpace(translated) ? value : translated;
                }
            }
            string separator = (parameter as string) switch
            {
                "RuntimeConnectionStatus" => ". ",
                "RuntimeConnectionCommandStatus" => ": ",
                "RuntimeConnectionStatusLines" => ". ",
                "RuntimeConnectionCommandStatusLines" => ": ",
                _ => null
            };
            if (separator != null)
            {
                var entries = OpenVisionLanguageService.GetEntries();
                foreach (string key in new[]
                {
                    "WpfModelDetails.RuntimeFollowOn.PythonModelRuntimeConnectionService.23",
                    "WpfModelDetails.RuntimeFollowOn.PythonModelRuntimeConnectionService.24",
                    "WpfModelDetails.RuntimeFollowOn.PythonModelRuntimeConnectionService.62",
                    "WpfModelDetails.RuntimeFollowOn.PythonModelRuntimeConnectionService.63",
                    "WpfModelDetails.RuntimeFollowOn.PythonModelRuntimeConnectionService.92",
                    "WpfModelDetails.RuntimeFollowOn.PythonModelRuntimeConnectionService.93",
                    "WpfModelDetails.RuntimeConnection.BuildUnetFolderConnection.117",
                    "WpfModelDetails.RuntimeConnection.BuildUnetFolderConnection.118",
                    "WpfModelDetails.RuntimeConnection.BuildPatchCoreConnection.140",
                    "WpfModelDetails.RuntimeConnection.BuildPatchCoreConnection.141",
                    "WpfModelDetails.RuntimeFollowOn.PythonModelRuntimeConnectionService.225",
                    "WpfModelDetails.RuntimeConnection.BuildUltralyticsPythonConnection.226"
                })
                {
                    OpenVisionLocalizationEntry entry = entries.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
                    if (entry == null) continue;
                    var summaries = new[] { entry.Korean, entry.English }
                        .Concat(OpenVisionLanguageService.GetPreviousKoreanDefaults(key))
                        .Concat(defaultEntries.TryGetValue(key, out OpenVisionLocalizationEntry defaults)
                            ? new[] { defaults.Korean, defaults.English }
                            : Array.Empty<string>())
                        .Where(summary => !string.IsNullOrWhiteSpace(summary)).Distinct(StringComparer.Ordinal)
                        .OrderByDescending(summary => summary.Length);
                    foreach (string summary in summaries)
                    {
                        if (!value.StartsWith(summary + separator, StringComparison.Ordinal)) continue;
                        string translatedSummary = OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean ? entry.Korean : entry.English;
                        string outputSeparator = parameter is "RuntimeConnectionStatusLines" or "RuntimeConnectionCommandStatusLines" ? Environment.NewLine : separator;
                        return (string.IsNullOrWhiteSpace(translatedSummary) ? summary : translatedSummary) + outputSeparator + Translate(value.Substring(summary.Length + separator.Length));
                    }
                }
            }
            return Translate(value);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        public string Translate(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (Regex.IsMatch(value, @"\A(?:[A-Za-z]:[\\/]|\\\\|/)", RegexOptions.CultureInvariant)) return value;
            bool isKorean = OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean;
            OpenVisionLocalizationEntry[] entries = OpenVisionLanguageService.GetEntries().Where(entry => entry.Key.StartsWith("WpfModelDetails.", StringComparison.Ordinal)).ToArray();
            var templates = entries.SelectMany(entry => new[] { entry.Korean, entry.English }
                .Concat(OpenVisionLanguageService.GetPreviousKoreanDefaults(entry.Key))
                .Concat(defaultEntries.TryGetValue(entry.Key, out OpenVisionLocalizationEntry defaults)
                    ? new[] { defaults.Korean, defaults.English }
                    : Array.Empty<string>())
                .Where(text => !string.IsNullOrWhiteSpace(text)).Distinct(StringComparer.Ordinal)
                .Select(text => (Entry: entry, Text: text))).OrderByDescending(template => template.Text.Length).ToArray();
            foreach (var template in templates)
            {
                if (!string.Equals(template.Text, value, StringComparison.Ordinal)) continue;
                string translated = isKorean ? template.Entry.Korean : template.Entry.English;
                return string.IsNullOrWhiteSpace(translated) ? value : translated;
            }
            if (!isKorean)
            {
                string exact = LocalizationTextRuntimeService.Translate(value);
                if (!string.Equals(exact, value, StringComparison.Ordinal)) return string.IsNullOrWhiteSpace(exact) ? value : exact;
            }
            foreach (var template in templates)
            {
                if (!template.Text.Contains("{0}", StringComparison.Ordinal)) continue;
                if (!templatePatterns.TryGetValue(template.Text, out Regex pattern))
                {
                    var expression = new StringBuilder(@"\A");
                    int offset = 0;
                    var argumentIndexes = new HashSet<int>();
                    bool isValidTemplate = true;
                    foreach (Match placeholder in Regex.Matches(template.Text, @"\{([0-9]+)\}"))
                    {
                        expression.Append(Regex.Escape(template.Text.Substring(offset, placeholder.Index - offset)));
                        if (!int.TryParse(placeholder.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int index))
                        {
                            isValidTemplate = false;
                            break;
                        }
                        expression.Append(argumentIndexes.Add(index) ? $"(?<arg{index}>.*?)" : $@"\k<arg{index}>");
                        offset = placeholder.Index + placeholder.Length;
                    }
                    if (!isValidTemplate) continue;
                    expression.Append(Regex.Escape(template.Text.Substring(offset))).Append(@"\z");
                    pattern = new Regex(expression.ToString(), RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(250));
                    templatePatterns.Add(template.Text, pattern);
                }
                Match match;
                try { match = pattern.Match(value); }
                catch (RegexMatchTimeoutException) { return value; }
                if (!match.Success) continue;
                object[] arguments = pattern.GetGroupNames().Where(name => name.StartsWith("arg", StringComparison.Ordinal)).OrderBy(name => int.Parse(name.Substring(3), CultureInfo.InvariantCulture)).Select(name => (object)match.Groups[name].Value).ToArray();
                string translated = isKorean ? template.Entry.Korean : template.Entry.English;
                if (string.IsNullOrWhiteSpace(translated)) return value;
                try { return string.Format(CultureInfo.InvariantCulture, translated, arguments); }
                catch (FormatException) { return value; }
            }
            return value;
        }
    }
}
