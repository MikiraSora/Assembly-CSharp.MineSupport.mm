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
            MineRuntime.PrepareRuntime(this);
            orig_Initialize(note);
            MineRuntime.BindRuntime(this, note, MonitorId, NoteIndex);
            MineRuntime.ApplyVisual(this, MineRuntime.IsMine(this));
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
    }
}
