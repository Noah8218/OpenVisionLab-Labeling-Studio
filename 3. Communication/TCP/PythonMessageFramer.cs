using System;
using System.Collections.Generic;
using System.Text;

namespace MvcVisionSystem._3._Communication.TCP
{
    public sealed class PythonMessageFramer
    {
        private static readonly string[] CommandNames =
        {
            PythonDetectionResultProtocol.ResultDefectCommand,
            nameof(CCommunicationLearning.CommandLearning.StartTraining),
            nameof(CCommunicationLearning.CommandLearning.StopTraining),
            nameof(CCommunicationLearning.CommandLearning.StartDefect),
            nameof(CCommunicationLearning.CommandLearning.StopDefect)
        };

        private readonly StringBuilder buffer = new StringBuilder();

        public IReadOnlyList<string> Append(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
            {
                return Array.Empty<string>();
            }

            buffer.Append(chunk);
            return Drain();
        }

        private IReadOnlyList<string> Drain()
        {
            var messages = new List<string>();

            while (buffer.Length > 0)
            {
                TrimLeadingWhitespace();
                if (buffer.Length == 0)
                {
                    break;
                }

                string current = buffer.ToString();
                if (TryReadDetectionMessage(current, out string detectionMessage, out int detectionLength))
                {
                    messages.Add(detectionMessage);
                    buffer.Remove(0, detectionLength);
                    continue;
                }

                if (TryReadJsonEnvelope(current, out string jsonMessage, out int jsonLength))
                {
                    messages.Add(jsonMessage);
                    buffer.Remove(0, jsonLength);
                    continue;
                }

                if (TryReadSimpleCommand(current, out string command, out int commandLength))
                {
                    messages.Add(command);
                    buffer.Remove(0, commandLength);
                    continue;
                }

                int lineBreakIndex = IndexOfLineBreak(current);
                if (lineBreakIndex >= 0)
                {
                    string line = current.Substring(0, lineBreakIndex).Trim();
                    buffer.Remove(0, lineBreakIndex + GetLineBreakLength(current, lineBreakIndex));
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        messages.Add(line);
                    }
                    continue;
                }

                if (IsPotentialCommandPrefix(current))
                {
                    break;
                }

                messages.Add(current);
                buffer.Clear();
            }

            return messages;
        }

        private static bool TryReadDetectionMessage(string current, out string message, out int length)
        {
            message = "";
            length = 0;

            string command = PythonDetectionResultProtocol.ResultDefectCommand;
            if (!current.StartsWith(command, StringComparison.Ordinal))
            {
                return false;
            }

            int payloadStart = command.Length;
            if (current.Length >= command.Length + LearningProtocol.PacketSeparator.Length &&
                string.Equals(current.Substring(command.Length, LearningProtocol.PacketSeparator.Length), LearningProtocol.PacketSeparator, StringComparison.Ordinal))
            {
                payloadStart = command.Length + LearningProtocol.PacketSeparator.Length;
            }

            while (payloadStart < current.Length && char.IsWhiteSpace(current[payloadStart]))
            {
                payloadStart++;
            }

            if (payloadStart >= current.Length)
            {
                return false;
            }

            char payloadStartChar = current[payloadStart];
            if (payloadStartChar != '[' && payloadStartChar != '{')
            {
                int lineBreakIndex = IndexOfLineBreak(current);
                if (lineBreakIndex < 0)
                {
                    return false;
                }

                message = current.Substring(0, lineBreakIndex).Trim();
                length = lineBreakIndex + GetLineBreakLength(current, lineBreakIndex);
                return true;
            }

            if (!TryFindBalancedJsonEnd(current, payloadStart, out int payloadEnd))
            {
                return false;
            }

            string payload = current.Substring(payloadStart, payloadEnd - payloadStart + 1);
            message = $"{command} {payload}";
            length = IncludeTrailingMessageDelimiter(current, payloadEnd + 1);
            return true;
        }

        private static bool TryReadJsonEnvelope(string current, out string message, out int length)
        {
            message = "";
            length = 0;

            if (current[0] != '{')
            {
                return false;
            }

            if (!TryFindBalancedJsonEnd(current, 0, out int payloadEnd))
            {
                return false;
            }

            message = current.Substring(0, payloadEnd + 1);
            length = IncludeTrailingMessageDelimiter(current, payloadEnd + 1);
            return true;
        }

        private static bool TryReadSimpleCommand(string current, out string command, out int length)
        {
            command = "";
            length = 0;

            foreach (string candidate in CommandNames)
            {
                if (string.Equals(candidate, PythonDetectionResultProtocol.ResultDefectCommand, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!current.StartsWith(candidate, StringComparison.Ordinal))
                {
                    continue;
                }

                if (current.Length > candidate.Length && !char.IsWhiteSpace(current[candidate.Length]))
                {
                    continue;
                }

                command = candidate;
                length = IncludeTrailingMessageDelimiter(current, candidate.Length);
                return true;
            }

            return false;
        }

        private static bool TryFindBalancedJsonEnd(string text, int startIndex, out int endIndex)
        {
            endIndex = -1;
            char open = text[startIndex];
            char close = open == '[' ? ']' : '}';
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = startIndex; i < text.Length; i++)
            {
                char ch = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (ch == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (ch == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                    continue;
                }

                if (ch == open)
                {
                    depth++;
                    continue;
                }

                if (ch == close)
                {
                    depth--;
                    if (depth == 0)
                    {
                        endIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        private void TrimLeadingWhitespace()
        {
            int removeCount = 0;
            while (removeCount < buffer.Length && char.IsWhiteSpace(buffer[removeCount]))
            {
                removeCount++;
            }

            if (removeCount > 0)
            {
                buffer.Remove(0, removeCount);
            }
        }

        private static int IncludeTrailingMessageDelimiter(string current, int length)
        {
            if (current.Length >= length + LearningProtocol.PacketSeparator.Length &&
                string.Equals(current.Substring(length, LearningProtocol.PacketSeparator.Length), LearningProtocol.PacketSeparator, StringComparison.Ordinal))
            {
                return length + LearningProtocol.PacketSeparator.Length;
            }

            if (current.Length > length && current[length] == '\r')
            {
                return current.Length > length + 1 && current[length + 1] == '\n' ? length + 2 : length + 1;
            }

            if (current.Length > length && current[length] == '\n')
            {
                return length + 1;
            }

            return length;
        }

        private static int IndexOfLineBreak(string current)
        {
            int cr = current.IndexOf('\r');
            int lf = current.IndexOf('\n');
            if (cr < 0) return lf;
            if (lf < 0) return cr;
            return Math.Min(cr, lf);
        }

        private static int GetLineBreakLength(string current, int lineBreakIndex)
        {
            if (current[lineBreakIndex] == '\r' && current.Length > lineBreakIndex + 1 && current[lineBreakIndex + 1] == '\n')
            {
                return 2;
            }

            return 1;
        }

        private static bool IsPotentialCommandPrefix(string current)
        {
            if (current.StartsWith(PythonDetectionResultProtocol.ResultDefectCommand, StringComparison.Ordinal))
            {
                return true;
            }

            foreach (string command in CommandNames)
            {
                if (command.StartsWith(current, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return current[0] == '{' || current[0] == '[';
        }
    }
}
