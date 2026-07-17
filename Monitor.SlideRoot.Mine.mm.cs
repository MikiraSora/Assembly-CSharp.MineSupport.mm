#pragma warning disable CS0626
using Manager;
using MineSupport;

namespace Monitor
{
    public class patch_SlideRoot : SlideRoot
    {
        public extern void orig_Initialize(NoteData note);

        public void Initialize(NoteData note)
        {
            orig_Initialize(note);
            MineRuntime.BindRuntime(this, note, MonitorId, NoteIndex);
            MineRuntime.ApplyVisual(this, MineRuntime.IsMine(this));
        }

        public extern void orig_Execute();

        public void Execute()
        {
            orig_Execute();
            if (MineRuntime.IsMine(this))
                MineRuntime.ApplyVisual(this, true);
        }

        protected extern void orig_NoteCheck();

        protected void NoteCheck()
        {
            MineRuntime.RunNoteCheck(this, orig_NoteCheck);
            if (IsEnd())
                MineRuntime.ReleaseRuntime(this);
        }

        protected extern void orig_PlayJudgeSe();

        protected void PlayJudgeSe()
        {
            if (MineRuntime.IsMine(this))
                return;

            orig_PlayJudgeSe();
        }

        protected extern void orig_ReserveSlideTouchSe();

        protected void ReserveSlideTouchSe()
        {
            if (MineRuntime.IsMine(this))
                return;

            orig_ReserveSlideTouchSe();
        }

        public extern void orig_SetForcePlayResult(int monitorId, NoteData note, NoteJudge.ETiming timing);

        public void SetForcePlayResult(int monitorId, NoteData note, NoteJudge.ETiming timing)
        {
            MineRuntime.RunFeedbackSuppressed(this, () => orig_SetForcePlayResult(monitorId, note, timing));
        }
    }
}
