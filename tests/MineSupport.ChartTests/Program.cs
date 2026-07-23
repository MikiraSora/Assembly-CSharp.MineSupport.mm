using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Manager;
using MineSupport;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            TestValidRecords();
            TestInvalidLocations();
            TestContainsTails();
            TestConnectedSlideOwnership();
            if (args.Length == 1)
                TestChartFile(args[0]);
            System.Console.WriteLine("MineSupport.ChartTests: PASS");
            return 0;
        }
        catch (Exception exception)
        {
            System.Console.Error.WriteLine("MineSupport.ChartTests: FAIL");
            System.Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void TestChartFile(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var records = new List<MA2Record>();
        var expectedMineCount = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var record = new MA2Record(lines[i]);
            if (!record.getType().isValid())
            {
                Require(MineTailParser.CountMineMarkers(lines[i]) == 0,
                    $"unknown record contains a Mine marker at {path}:{i + 1}");
                continue;
            }
            records.Add(record);
            if (record.getType().getCategory() == Ma2Category.MA2_Note)
                expectedMineCount++;
        }

        var recordList = new MA2RecordList(records);
        var map = MineChartLoader.CreateTailMap();
        bool hasMine;
        MineChartError error;
        Require(MineChartLoader.TryPrepare(path, recordList, map, out hasMine, out error), error?.ToString());
        Require(hasMine, "generated chart did not contain Mine records");
        Require(map.Count == expectedMineCount,
            $"generated chart Mine count mismatch: expected {expectedMineCount}, actual {map.Count}");
    }

    private static void TestValidRecords()
    {
        var records = CreateRecords(
            "NMTAP\t0\t0\t0\t!m",
            "NMHLD\t0\t24\t1\t96\t!m#12",
            "NMTTP\t0\t48\t2\tB\t0\tM1\t#12F600!m",
            "NMTHO\t0\t72\t3\t96\tB\t0\tM1\t!m#F600",
            "NMSTR\t1\t0\t4\t!m",
            "NMSI_\t1\t0\t4\t96\t384\t6\t!m#12F600",
            "NMTAP\t1\t24\t5\t#1!m!y",
            "NMTAP\t1\t48\t6\t!y!m#1",
            "NMTAP\t2\t0\t0");

        var map = MineChartLoader.CreateTailMap();
        bool hasMine;
        MineChartError error;
        Require(MineChartLoader.TryPrepare("valid.ma2", records, map, out hasMine, out error), error?.ToString());
        Require(hasMine, "valid chart did not report Mine notes");
        Require(map.Count == 8, "valid chart Mine count mismatch");
        Require(map[records[2]].SanitizedTail == "#12F600", "Soflan tail was not preserved");
        Require(map[records[5]].RecordOrdinal == 6, "record ordinal mismatch");
        Require(map[records[6]].SanitizedTail == "#1!y", "suffix modifier was not preserved");
        Require(map[records[7]].SanitizedTail == "!y#1", "prefix modifier was not preserved");
    }

    private static void TestInvalidLocations()
    {
        AssertRejected("NMTAP\t0\t!m\t0", "single final tab-separated");
        AssertRejected("NMTAP\t0\t0\t0\t#12\t!m", "single final tab-separated");
        AssertRejected("BPM\t0\t0\t120\t!m", "only valid on MA2 note records");
    }

    private static void TestContainsTails()
    {
        var records = CreateRecords(
            "NMTAP\t0\t0\t0\t!m!m",
            "NMTAP\t0\t24\t1\t!m##12",
            "NMTAP\t0\t48\t2\t!m#12F0",
            "NMTAP\t0\t72\t3\tabc!m",
            "NMTAP\t1\t0\t4\t!M");
        var map = MineChartLoader.CreateTailMap();
        bool hasMine;
        MineChartError error;
        Require(MineChartLoader.TryPrepare("contains.ma2", records, map, out hasMine, out error), error?.ToString());
        Require(hasMine, "Contains-based chart did not report Mine notes");
        Require(map.Count == 4, "Contains-based Mine count mismatch");
        Require(map[records[0]].SanitizedTail == string.Empty, "duplicate !m was not fully removed");
        Require(map[records[1]].SanitizedTail == "##12", "non-Mine modifiers were changed");
        Require(map[records[2]].SanitizedTail == "#12F0", "Soflan validation leaked into Mine parsing");
        Require(map[records[3]].SanitizedTail == "abc", "embedded !m was not removed");
        Require(!map.ContainsKey(records[4]), "uppercase !M was treated as Mine");
    }

    private static void TestConnectedSlideOwnership()
    {
        var root = CreateNote(NotesTypeID.Def.Slide, true, 1, "!m");
        var child = CreateNote(NotesTypeID.Def.ConnectSlide, true, 2, "!m");
        var grandchild = CreateNote(NotesTypeID.Def.ConnectSlide, true, 3, "!m");
        root.child.Add(child);
        child.child.Add(grandchild);
        var noHeadChain = new NoteDataList { root, child, grandchild };
        MineChartError error;
        Require(MineChartLoader.TryNormalizeSlideChains("no-head.ma2", noHeadChain, out error),
            "valid multi-segment no-head connected Slide was rejected: " + error);

        var inconsistentRoot = CreateNote(NotesTypeID.Def.Slide, true, 4, "!m");
        var consistentChild = CreateNote(NotesTypeID.Def.ConnectSlide, true, 5, "!m");
        var inconsistentChild = CreateNote(NotesTypeID.Def.ConnectSlide, false, 6, string.Empty);
        inconsistentRoot.child.Add(consistentChild);
        consistentChild.child.Add(inconsistentChild);
        Require(!MineChartLoader.TryNormalizeSlideChains(
                "inconsistent.ma2",
                new NoteDataList { inconsistentRoot, consistentChild, inconsistentChild },
                out error),
            "deep inconsistent connected Slide was accepted");

        var orphan = CreateNote(NotesTypeID.Def.ConnectSlide, true, 7, "!m");
        Require(!MineChartLoader.TryNormalizeSlideChains("orphan.ma2", new NoteDataList { orphan }, out error),
            "orphan Mine ConnectSlide was accepted");
    }

    private static patch_NoteData CreateNote(NotesTypeID.Def type, bool isMine, int ordinal, string tail)
    {
        return new patch_NoteData
        {
            type = new NotesTypeID(type),
            isMine = isMine,
            mineRecordOrdinal = ordinal,
            mineRawTail = tail
        };
    }

    private static void AssertRejected(string line, string expectedReasonFragment)
    {
        var records = CreateRecords(line);
        var map = MineChartLoader.CreateTailMap();
        bool hasMine;
        MineChartError error;
        Require(!MineChartLoader.TryPrepare("invalid.ma2", records, map, out hasMine, out error), line + " was accepted");
        Require(error != null, line + " did not return an error");
        Require(error.RecordOrdinal == 1, line + " returned the wrong record ordinal");
        Require(error.Reason.IndexOf(expectedReasonFragment, StringComparison.OrdinalIgnoreCase) >= 0,
            line + " returned unexpected reason: " + error.Reason);
    }

    private static MA2RecordList CreateRecords(params string[] lines)
    {
        var records = new List<MA2Record>();
        for (var i = 0; i < lines.Length; i++)
        {
            var record = new MA2Record(lines[i]);
            Require(record.getType().isValid(), "invalid test record: " + lines[i]);
            records.Add(record);
        }

        return new MA2RecordList(records);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

namespace Manager
{
    public class patch_NoteData : NoteData
    {
        public bool isMine;
        public int mineRecordOrdinal;
        public string mineRawTail;
    }
}

namespace MineSupport
{
    internal static class MineRuntime
    {
        internal static bool IsMine(NoteData note)
        {
            return note != null && ((patch_NoteData)note).isMine;
        }
    }
}
