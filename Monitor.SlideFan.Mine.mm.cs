#pragma warning disable CS0626
using Manager;
using MineSupport;

namespace Monitor
{
    public class patch_SlideFan : SlideFan
    {
        public extern void orig_Initialize(NoteData note);

        public void Initialize(NoteData note)
        {
            orig_Initialize(note);
            MineRuntime.BindRuntime(this, note, MonitorId, NoteIndex);
            MineRuntime.ApplyVisual(this, MineRuntime.IsMine(this));
        }

        protected extern void orig_NoteCheck();

        protected void NoteCheck()
        {
            MineRuntime.RunNoteCheck(this, orig_NoteCheck);
            if (IsEnd())
                MineRuntime.ReleaseRuntime(this);
        }
    }
}
