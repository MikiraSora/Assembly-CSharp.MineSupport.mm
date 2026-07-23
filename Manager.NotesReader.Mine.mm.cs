#pragma warning disable CS0626
using System.Collections.Generic;
using MineSupport;

namespace Manager
{
    public class patch_NotesReader : NotesReader
    {
        private Dictionary<MA2Record, MineTailInfo> mineTailMap = MineChartLoader.CreateTailMap();

        protected extern bool orig_loadNote(MA2Record rec, int index, ref int noteIndex, ref int slideIndex);

        protected bool loadNote(MA2Record rec, int index, ref int noteIndex, ref int slideIndex)
        {
            var notes = GetNoteList();
            var countBefore = notes == null ? 0 : notes.Count;
            MineTailInfo tailInfo = null;
            var hasMine = !ReferenceEquals(rec, null) && mineTailMap.TryGetValue(rec, out tailInfo);
            var tailIndex = hasMine ? rec.getType().getParamNum() : -1;
            string originalTail = null;

            if (hasMine)
            {
                originalTail = rec._str[tailIndex];
                rec._str[tailIndex] = tailInfo.SanitizedTail;
            }

            bool result;
            try
            {
                result = orig_loadNote(rec, index, ref noteIndex, ref slideIndex);
            }
            finally
            {
                if (hasMine)
                    rec._str[tailIndex] = originalTail;
            }

            if (result && notes != null && notes.Count > countBefore)
                __MineApplyTailInfo(notes, hasMine, tailInfo);

            return result;
        }

        private static void __MineApplyTailInfo(NoteDataList notes, bool hasMine, MineTailInfo tailInfo)
        {
            var mineNote = (patch_NoteData)(object)notes[notes.Count - 1];
            mineNote.isMine = hasMine;
            if (!hasMine)
                return;

            mineNote.mineRecordOrdinal = tailInfo.RecordOrdinal;
            mineNote.mineRawTail = tailInfo.RawTail;
        }

        protected extern void orig_calcEach();

        protected void calcEach()
        {
            var notes = GetNoteList();
            if (notes == null)
            {
                orig_calcEach();
                return;
            }

            var removed = new List<MineNotePlacement>();
            for (var i = notes.Count - 1; i >= 0; i--)
            {
                if (!MineRuntime.IsMine(notes[i]))
                    continue;

                removed.Add(new MineNotePlacement(i, notes[i]));
                notes.RemoveAt(i);
            }

            try
            {
                orig_calcEach();
            }
            finally
            {
                removed.Sort((left, right) => left.Index.CompareTo(right.Index));
                for (var i = 0; i < removed.Count; i++)
                {
                    var placement = removed[i];
                    placement.Note.isEach = false;
                    placement.Note.indexEach = -1;
                    placement.Note.indexTouchGroup = -1;
                    placement.Note.parent = null;
                    placement.Note.eachChild.Clear();
                    notes.Insert(placement.Index, placement.Note);
                }
            }
        }

        private extern bool orig_loadMa2Main(string fileName, MA2RecordList records, LoadType loadType);

        private bool loadMa2Main(string fileName, MA2RecordList records, LoadType loadType)
        {
            mineTailMap.Clear();
            if (loadType == LoadType.LOAD_FULL)
            {
                bool hasMine;
                MineChartError chartError;
                if (!MineChartLoader.TryPrepare(fileName, records, mineTailMap, out hasMine, out chartError))
                {
                    PatchLog.Error(chartError.ToString());
                    MineRuntime.ClearTransientState();
                    init(_playerID);
                    return false;
                }

                string resourceError = string.Empty;
                if (hasMine && !MineVisual.EnsureAvailable(out resourceError))
                {
                    PatchLog.ErrorOnce(
                        "mine-visual-unavailable",
                        $"[Mine] chart rejected: path={fileName}, record=0, type=RESOURCE, tail=<none>, reason={resourceError}");
                    MineRuntime.ClearTransientState();
                    init(_playerID);
                    return false;
                }
            }

            var result = orig_loadMa2Main(fileName, records, loadType);
            if (!result || loadType != LoadType.LOAD_FULL)
                return result;
            if (mineTailMap.Count == 0)
                return true;

            MineChartError slideError;
            if (!MineChartLoader.TryNormalizeSlideChains(fileName, GetNoteList(), out slideError))
            {
                PatchLog.Error(slideError.ToString());
                MineRuntime.ClearTransientState();
                init(_playerID);
                return false;
            }

            return true;
        }

        private readonly struct MineNotePlacement
        {
            internal MineNotePlacement(int index, NoteData note)
            {
                Index = index;
                Note = note;
            }

            internal int Index { get; }

            internal NoteData Note { get; }
        }
    }
}
