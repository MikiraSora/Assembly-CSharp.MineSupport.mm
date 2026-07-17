#pragma warning disable CS0626
using Manager;
using MineSupport;

namespace Monitor
{
    public abstract class patch_NoteBase : NoteBase
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
        }

        protected extern void orig_EndNote();

        protected void EndNote()
        {
            MineRuntime.RunFeedbackSuppressed(this, orig_EndNote);
            MineRuntime.ReleaseRuntime(this);
        }

        protected extern void orig_PlayJudgeSe();

        protected void PlayJudgeSe()
        {
            if (MineRuntime.IsMine(this))
                return;

            orig_PlayJudgeSe();
        }

        public extern void orig_SetForcePlayResult(int monitorId, NoteData note, NoteJudge.ETiming timing);

        public void SetForcePlayResult(int monitorId, NoteData note, NoteJudge.ETiming timing)
        {
            MineRuntime.RunFeedbackSuppressed(this, () => orig_SetForcePlayResult(monitorId, note, timing));
        }
    }
}
