#pragma warning disable CS0626
using MineSupport;

namespace Monitor
{
    public class patch_TouchEffect : TouchEffect
    {
        public extern void orig_Initialize(NoteJudge.ETiming judge);

        public void Initialize(NoteJudge.ETiming judge)
        {
            if (MineRuntime.FeedbackSuppressed)
                return;

            orig_Initialize(judge);
        }

        public extern void orig_InitializeEx(NoteJudge.ETiming judge);

        public void InitializeEx(NoteJudge.ETiming judge)
        {
            if (MineRuntime.FeedbackSuppressed)
                return;

            orig_InitializeEx(judge);
        }

        public extern void orig_InitializeCenter(NoteJudge.ETiming judge);

        public void InitializeCenter(NoteJudge.ETiming judge)
        {
            if (MineRuntime.FeedbackSuppressed)
                return;

            orig_InitializeCenter(judge);
        }

        public extern void orig_InitializeHold(NoteJudge.ETiming judge);

        public void InitializeHold(NoteJudge.ETiming judge)
        {
            if (MineRuntime.FeedbackSuppressed)
                return;

            orig_InitializeHold(judge);
        }

        public extern void orig_FinishHold(NoteJudge.ETiming judge);

        public void FinishHold(NoteJudge.ETiming judge)
        {
            if (MineRuntime.FeedbackSuppressed)
                return;

            orig_FinishHold(judge);
        }

        public extern void orig_InitializeBreak(NoteJudge.ETiming judge);

        public void InitializeBreak(NoteJudge.ETiming judge)
        {
            if (MineRuntime.FeedbackSuppressed)
                return;

            orig_InitializeBreak(judge);
        }
    }
}
