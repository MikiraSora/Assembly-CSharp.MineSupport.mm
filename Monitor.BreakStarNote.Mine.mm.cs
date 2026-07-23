#pragma warning disable CS0626
using Manager;
using MineSupport;

namespace Monitor
{
    public class patch_BreakStarNote : BreakStarNote
    {
        public extern void orig_Initialize(NoteData note);

        public void Initialize(NoteData note)
        {
            orig_Initialize(note);
            MineRuntime.ApplyVisual(this, MineRuntime.IsMine(this), ExObj);
        }

        public extern void orig_Execute();

        public void Execute()
        {
            orig_Execute();
            if (MineRuntime.IsMine(this))
                MineRuntime.ApplyVisual(this, true);
        }

        public extern void orig_ExecuteSlideBreakEffect();

        public void ExecuteSlideBreakEffect()
        {
            if (MineRuntime.IsMine(this))
                return;

            orig_ExecuteSlideBreakEffect();
        }
    }
}
