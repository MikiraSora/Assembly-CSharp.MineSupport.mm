#pragma warning disable CS0626
using Manager;
using MineSupport;

namespace Monitor
{
    public class patch_BreakNote : BreakNote
    {
        public extern void orig_Initialize(NoteData note);

        public void Initialize(NoteData note)
        {
            orig_Initialize(note);
            if (GetType() == typeof(BreakNote))
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

        protected extern float orig_GetNoteYPosition();

        protected float GetNoteYPosition()
        {
            var y = orig_GetNoteYPosition();
            if (MineRuntime.IsMine(this) && EffectSprite != null)
            {
                var color = EffectSprite.color;
                color.a = 0f;
                EffectSprite.color = color;
            }

            return y;
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
                        JudgeGradeObject.InitializeBreak(NoteJudge.ETiming.TooLate, JudgeTimingDiffMsec, JudgeType);
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
