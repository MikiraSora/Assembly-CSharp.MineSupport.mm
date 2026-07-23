using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Manager;

namespace MineSupport
{
    internal sealed class MineTailInfo
    {
        internal int RecordOrdinal;
        internal string RecordType;
        internal string RawTail;
        internal string SanitizedTail;
    }

    internal sealed class MineChartError
    {
        internal string ChartPath;
        internal int RecordOrdinal;
        internal string RecordType;
        internal string RawTail;
        internal string Reason;

        public override string ToString()
        {
            return $"[Mine] chart rejected: path={ChartPath}, record={RecordOrdinal}, type={RecordType}, tail={RawTail}, reason={Reason}";
        }
    }

    internal static class MineChartLoader
    {
        internal static Dictionary<MA2Record, MineTailInfo> CreateTailMap()
        {
            return new Dictionary<MA2Record, MineTailInfo>(ReferenceEqualityComparer<MA2Record>.Instance);
        }

        internal static bool TryPrepare(
            string chartPath,
            MA2RecordList records,
            Dictionary<MA2Record, MineTailInfo> tails,
            out bool hasMine,
            out MineChartError error)
        {
            hasMine = false;
            error = null;
            tails.Clear();

            if (records == null)
                return true;

            for (var recordIndex = 0; recordIndex < records.Count; recordIndex++)
            {
                var record = records[recordIndex];
                if (ReferenceEquals(record, null) || record._str == null)
                    continue;

                var markerField = -1;
                var markerInMultipleFields = false;
                for (var fieldIndex = 0; fieldIndex < record._str.Count; fieldIndex++)
                {
                    var str = record._str[fieldIndex];
                    if (str == null || !str.Contains("!m"))
                        continue;

                    if (markerField >= 0 && markerField != fieldIndex)
                        markerInMultipleFields = true;
                    else
                        markerField = fieldIndex;
                }

                if (markerField < 0)
                    continue;

                var recordType = record.getType().getEnumName();
                var rawTail = markerField >= 0 && markerField < record._str.Count
                    ? record._str[markerField]
                    : "<missing>";

                if (record.getType().getCategory() != Ma2Category.MA2_Note)
                {
                    error = CreateError(chartPath, recordIndex, recordType, rawTail, "!m is only valid on MA2 note records");
                    return false;
                }

                var tailIndex = record.getType().getParamNum();
                if (markerInMultipleFields || record._str.Count != tailIndex + 1 || markerField != tailIndex)
                {
                    error = CreateError(
                        chartPath,
                        recordIndex,
                        recordType,
                        rawTail,
                        "!m must appear in the single final tab-separated modifier field");
                    return false;
                }

                MineTailParseResult parseResult;
                string reason;
                if (!MineTailParser.TryParse(rawTail, out parseResult, out reason) || !parseResult.IsMine)
                {
                    error = CreateError(chartPath, recordIndex, recordType, rawTail, reason);
                    return false;
                }

                tails.Add(record, new MineTailInfo
                {
                    RecordOrdinal = recordIndex + 1,
                    RecordType = recordType,
                    RawTail = rawTail,
                    SanitizedTail = parseResult.SanitizedTail
                });
                hasMine = true;
            }

            return true;
        }

        internal static bool TryNormalizeSlideChains(string chartPath, NoteDataList notes, out MineChartError error)
        {
            error = null;
            if (notes == null)
                return true;

            var ownedConnectSegments = new HashSet<NoteData>(ReferenceEqualityComparer<NoteData>.Instance);

            for (var i = 0; i < notes.Count; i++)
            {
                var root = notes[i];
                if (root == null || !root.type.isSlide() || root.type.isConnectSlide())
                    continue;

                var rootMine = MineRuntime.IsMine(root);
                var pending = new Stack<NoteData>();
                for (var childIndex = 0; childIndex < root.child.Count; childIndex++)
                {
                    var child = root.child[childIndex];
                    if (child != null && child.type.isConnectSlide())
                        pending.Push(child);
                }

                while (pending.Count != 0)
                {
                    var child = pending.Pop();


                    if (MineRuntime.IsMine(child) != rootMine)
                    {
                        error = CreateNoteError(
                            chartPath,
                            child,
                            "connected Slide segments must all share the same !m body state");
                        return false;
                    }

                    if (!ownedConnectSegments.Add(child))
                        continue;

                    ((patch_NoteData)(object)child).isMine = rootMine;
                    for (var nestedIndex = 0; nestedIndex < child.child.Count; nestedIndex++)
                    {
                        var nested = child.child[nestedIndex];
                        if (nested != null && nested.type.isConnectSlide())
                            pending.Push(nested);
                    }
                }
            }

            for (var i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                if (note != null
                    && note.type.isConnectSlide()
                    && MineRuntime.IsMine(note)
                    && !ownedConnectSegments.Contains(note))
                {
                    error = CreateNoteError(chartPath, note, "Mine ConnectSlide segment has no owning Slide chain");
                    return false;
                }
            }

            return true;
        }

        private static MineChartError CreateError(
            string chartPath,
            int zeroBasedRecordIndex,
            string recordType,
            string rawTail,
            string reason)
        {
            return new MineChartError
            {
                ChartPath = chartPath ?? string.Empty,
                RecordOrdinal = zeroBasedRecordIndex + 1,
                RecordType = recordType ?? string.Empty,
                RawTail = rawTail ?? string.Empty,
                Reason = string.IsNullOrEmpty(reason) ? "invalid Mine modifier tail" : reason
            };
        }

        private static MineChartError CreateNoteError(string chartPath, NoteData note, string reason)
        {
            var mineNote = (patch_NoteData)(object)note;
            return new MineChartError
            {
                ChartPath = chartPath ?? string.Empty,
                RecordOrdinal = mineNote.mineRecordOrdinal,
                RecordType = note.type.getEnumName(),
                RawTail = mineNote.mineRawTail ?? string.Empty,
                Reason = reason
            };
        }
    }

    internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        internal static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

        private ReferenceEqualityComparer()
        {
        }

        public bool Equals(T left, T right)
        {
            return ReferenceEquals(left, right);
        }

        public int GetHashCode(T value)
        {
            return RuntimeHelpers.GetHashCode(value);
        }
    }
}
