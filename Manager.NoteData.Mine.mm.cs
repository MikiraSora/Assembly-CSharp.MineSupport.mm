namespace Manager
{
    public class patch_NoteData : NoteData
    {
        public bool isMine;

        public extern void orig_clear();

        public void clear()
        {
            orig_clear();
            isMine = false;
        }
    }
}
