using System;
using MineSupport;

internal static class Program
{
    private static int Main()
    {
        try
        {
            TestAcceptedTails();
            TestContainsSemantics();
            TestSourcePolicy();
            Console.WriteLine("MineSupport.CoreTests: PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("MineSupport.CoreTests: FAIL");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void TestAcceptedTails()
    {
        var cases = new[]
        {
            new TailCase("!m", string.Empty),
            new TailCase("!m#12", "#12"),
            new TailCase("!m#F600", "#F600"),
            new TailCase("!m#12F600", "#12F600"),
            new TailCase("#12F600!m", "#12F600"),
            new TailCase("#12!mF600", "#12F600"),
            new TailCase("#F!m", "#F"),
            new TailCase("#1!m!y", "#1!y"),
            new TailCase("!y!m#1", "!y#1"),
            new TailCase("!m!m", string.Empty),
            new TailCase("abc!m", "abc"),
            new TailCase("!m##12", "##12"),
            new TailCase("!m#12F0", "#12F0")
        };

        for (var i = 0; i < cases.Length; i++)
        {
            MineTailParseResult result;
            string reason;
            Require(MineTailParser.TryParse(cases[i].Tail, out result, out reason), cases[i].Tail + ": " + reason);
            Require(result.IsMine, cases[i].Tail + " did not set IsMine");
            Require(result.SanitizedTail == cases[i].SanitizedTail, cases[i].Tail + " normalized incorrectly");
        }

        MineTailParseResult ordinary;
        string ordinaryReason;
        Require(MineTailParser.TryParse("#12F600", out ordinary, out ordinaryReason), ordinaryReason);
        Require(!ordinary.IsMine, "ordinary soflan tail was treated as Mine");
    }

    private static void TestContainsSemantics()
    {
        MineTailParseResult uppercase;
        string uppercaseReason;
        Require(MineTailParser.TryParse("!M", out uppercase, out uppercaseReason), uppercaseReason);
        Require(!uppercase.IsMine, "uppercase !M was treated as Mine");

        MineTailParseResult nullTail;
        string nullReason;
        Require(!MineTailParser.TryParse(null, out nullTail, out nullReason), "null tail was accepted");
        Require(!string.IsNullOrEmpty(nullReason), "null tail did not report a reason");

        Require(MineTailParser.CountMineMarkers("!m!M!m") == 2,
            "Mine marker count was not exact-case");
    }

    private static void TestSourcePolicy()
    {
        var convertingSources = new[]
        {
            MineResultSource.Live,
            MineResultSource.ForcedTimeout,
            MineResultSource.NaturalFinish
        };
        for (var i = 0; i < convertingSources.Length; i++)
        {
            Require(MinePolicyCore.ShouldConvert(true, convertingSources[i], true), convertingSources[i] + " did not convert");
            Require(!MinePolicyCore.ShouldConvert(true, convertingSources[i], false), convertingSources[i] + " ignored target mismatch");
        }

        Require(!MinePolicyCore.ShouldConvert(true, MineResultSource.Ghost, true), "Ghost result was converted");
        Require(!MinePolicyCore.ShouldConvert(true, MineResultSource.Generated, true), "Generated result was converted");
        Require(!MinePolicyCore.ShouldConvert(false, MineResultSource.Live, true), "ordinary result was converted");
        Require(MinePolicyCore.ConvertTiming(0, 0, 14, 7) == 7, "TooFast mapping failed");
        Require(MinePolicyCore.ConvertTiming(14, 0, 14, 7) == 7, "TooLate mapping failed");
        Require(MinePolicyCore.ConvertTiming(5, 0, 14, 7) == 14, "non-miss mapping failed");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct TailCase
    {
        internal TailCase(string tail, string sanitizedTail)
        {
            Tail = tail;
            SanitizedTail = sanitizedTail;
        }

        internal string Tail { get; }
        internal string SanitizedTail { get; }
    }
}
