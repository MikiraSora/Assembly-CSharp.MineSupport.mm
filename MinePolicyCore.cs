namespace MineSupport
{
    internal enum MineResultSource
    {
        None,
        Live,
        ForcedTimeout,
        NaturalFinish,
        Ghost,
        Generated
    }

    internal static class MinePolicyCore
    {
        internal static bool ShouldConvert(bool isMine, MineResultSource source, bool targetMatches)
        {
            if (!isMine || !targetMatches)
                return false;

            return source == MineResultSource.Live
                || source == MineResultSource.ForcedTimeout
                || source == MineResultSource.NaturalFinish;
        }

        internal static int ConvertTiming(int timing, int tooFast, int tooLate, int critical)
        {
            return timing == tooFast || timing == tooLate ? critical : tooLate;
        }
    }
}
