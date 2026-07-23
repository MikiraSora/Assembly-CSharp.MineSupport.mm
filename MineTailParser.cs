using System;

namespace MineSupport
{
    internal readonly struct MineTailParseResult
    {
        internal MineTailParseResult(bool isMine, string sanitizedTail)
        {
            IsMine = isMine;
            SanitizedTail = sanitizedTail ?? string.Empty;
        }

        internal bool IsMine { get; }

        internal string SanitizedTail { get; }
    }

    internal static class MineTailParser
    {
        private const string MineMarker = "!m";

        internal static bool TryParse(string tail, out MineTailParseResult result, out string reason)
        {
            result = new MineTailParseResult(false, tail);
            reason = string.Empty;

            if (tail == null)
            {
                reason = "modifier tail is null";
                return false;
            }

            if (!tail.Contains(MineMarker))
                return true;

            result = new MineTailParseResult(true, tail.Replace(MineMarker, string.Empty));
            return true;
        }

        internal static int CountMineMarkers(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            var count = 0;
            var startIndex = 0;
            while (startIndex <= value.Length - MineMarker.Length)
            {
                var markerIndex = value.IndexOf(MineMarker, startIndex, StringComparison.Ordinal);
                if (markerIndex < 0)
                    break;

                count++;
                startIndex = markerIndex + MineMarker.Length;
            }

            return count;
        }
    }
}
