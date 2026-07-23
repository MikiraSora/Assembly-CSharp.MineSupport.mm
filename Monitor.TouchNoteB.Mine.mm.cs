#pragma warning disable CS0626
using Manager;
using MineSupport;

namespace Monitor
{
    public class patch_TouchNoteB : TouchNoteB
    {
        public extern void orig_Initialize(NoteData note);

        public void Initialize(NoteData note)
        {
            orig_Initialize(note);
            if (GetType() == typeof(TouchNoteB))
                MineRuntime.ApplyVisual(this, MineRuntime.IsMine(this), ExObj);
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
        }

        protected extern void orig_EndNote();

        protected void EndNote()
        {
            var scope = MineRuntime.EnterFeedback(this);
            try
            {
                orig_EndNote();
            }
            finally
            {
                MineRuntime.Exit(scope);
            }

            try
            {
                if (MineRuntime.TryBeginLiveMiss(this))
                {
                    var feedbackScope = MineRuntime.EnterVisibleMineFeedback();
                    try
                    {
                        JudgeGradeObject.Initialize(NoteJudge.ETiming.TooLate, JudgeTimingDiffMsec, NoteJudge.EJudgeType.Touch);
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

        protected extern void orig_PlayJudgeSe();

        protected void PlayJudgeSe()
        {
            if (MineRuntime.IsMine(this))
                return;

            orig_PlayJudgeSe();
        }

        protected extern void orig_ReserveTouchHoldLoopSe(NoteJudge.JudgeBox judge, bool loopDisable);

        protected void ReserveTouchHoldLoopSe(NoteJudge.JudgeBox judge, bool loopDisable)
        {
            if (MineRuntime.IsMine(this))
                return;

            orig_ReserveTouchHoldLoopSe(judge, loopDisable);
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
