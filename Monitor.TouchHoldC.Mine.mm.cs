#pragma warning disable CS0626
using Manager;
using MineSupport;

namespace Monitor
{
    public class patch_TouchHoldC : TouchHoldC
    {
        public extern void orig_Initialize(NoteData note);

        public void Initialize(NoteData note)
        {
            orig_Initialize(note);
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
        }

        protected extern void orig_EndNote();

        protected void EndNote()
        {
            MineRuntime.RunFeedbackSuppressed(this, orig_EndNote);
            MineRuntime.ReleaseRuntime(this);
        }

        protected extern void orig_PlayJudgeHeadSe();

        protected void PlayJudgeHeadSe()
        {
            if (MineRuntime.IsMine(this))
                return;

            orig_PlayJudgeHeadSe();
        }

        public extern void orig_SetForcePlayResult(int monitorId, NoteData note, NoteJudge.ETiming timing);

        public void SetForcePlayResult(int monitorId, NoteData note, NoteJudge.ETiming timing)
        {
            MineRuntime.RunFeedbackSuppressed(this, () => orig_SetForcePlayResult(monitorId, note, timing));
        }
    }
}
