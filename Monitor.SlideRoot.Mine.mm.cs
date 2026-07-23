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
            MineRuntime.PrepareRuntime(this);
            orig_Initialize(note);
            MineRuntime.BindRuntime(this, note, MonitorId, NoteIndex);
            if (GetType() == typeof(SlideRoot))
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
            var scope = MineRuntime.EnterNoteCheck(this);
            try
            {
                orig_NoteCheck();
            }
            finally
            {
                MineRuntime.Exit(scope);
            }

            if (IsEnd())
            {
                try
                {
                    if (MineRuntime.TryBeginLiveMiss(this))
                    {
                        var feedbackScope = MineRuntime.EnterVisibleMineFeedback();
                        try
                        {
                            JudgeObj.Initialize(NoteJudge.ETiming.TooLate, JudgeTimingDiffMsec, false);
                        }
                        finally
                        {
                            MineRuntime.Exit(feedbackScope);
                        }
                    }
                }
                finally
                {
                    MineRuntime.ReleaseRuntime(this);
                }
            }
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

        protected extern void orig_UpdateBreakEffect();

        protected void UpdateBreakEffect()
        {
            if (MineRuntime.IsMine(this))
                return;

            orig_UpdateBreakEffect();
        }

        public extern void orig_SetForcePlayResult(int monitorId, NoteData note, NoteJudge.ETiming timing);

        public void SetForcePlayResult(int monitorId, NoteData note, NoteJudge.ETiming timing)
        {
            var scope = MineRuntime.EnterForcedTimeout(monitorId, note);
            try
            {
                orig_SetForcePlayResult(monitorId, note, timing);
            }
            finally
            {
                MineRuntime.Exit(scope);
            }
        }
    }
}
