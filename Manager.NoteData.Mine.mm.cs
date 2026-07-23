namespace Manager
{
    public class patch_NoteData : NoteData
    {
        public bool isMine;

        public int mineRecordOrdinal;

        public string mineRawTail;

        public extern void orig_clear();

        public void clear()
        {
            orig_clear();
            isMine = false;
            mineRecordOrdinal = 0;
            mineRawTail = string.Empty;
        }
    }
}
