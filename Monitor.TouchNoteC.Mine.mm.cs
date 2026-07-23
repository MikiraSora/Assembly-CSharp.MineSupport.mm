#pragma warning disable CS0626
using Manager;
using MineSupport;

namespace Monitor
{
    public class patch_TouchNoteC : TouchNoteC
    {
        public extern void orig_Initialize(NoteData note);

        public void Initialize(NoteData note)
        {
            orig_Initialize(note);
            if (GetType() == typeof(TouchNoteC))
                MineRuntime.ApplyVisual(this, MineRuntime.IsMine(this), ExObj);
        }
    }
}
