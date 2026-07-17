#pragma warning disable CS0626
using MineSupport;

namespace Manager
{
    public class patch_NotesReader : NotesReader
    {
        protected extern bool orig_loadNote(MA2Record rec, int index, ref int noteIndex, ref int slideIndex);

        protected bool loadNote(MA2Record rec, int index, ref int noteIndex, ref int slideIndex)
        {
            var notes = GetNoteList();
            var countBefore = notes == null ? 0 : notes.Count;
            var result = orig_loadNote(rec, index, ref noteIndex, ref slideIndex);
            if (result && notes != null && notes.Count > countBefore)
            {
                ((patch_NoteData)(object)notes[notes.Count - 1]).isMine = MineRuntime.HasMineMarker(rec);
            }

            return result;
        }

        private extern bool orig_loadMa2Main(string fileName, MA2RecordList records, LoadType loadType);

        private bool loadMa2Main(string fileName, MA2RecordList records, LoadType loadType)
        {
            var result = orig_loadMa2Main(fileName, records, loadType);
            MineRuntime.NormalizeEach(GetNoteList());
            return result;
        }
    }
}
