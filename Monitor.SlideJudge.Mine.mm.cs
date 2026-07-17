#pragma warning disable CS0626
using MineSupport;

namespace Monitor
{
    public class patch_SlideJudge : SlideJudge
    {
        public extern void orig_Initialize(NoteJudge.ETiming judge, float msec, bool isBreak);

        public void Initialize(NoteJudge.ETiming judge, float msec, bool isBreak)
        {
            if (MineRuntime.FeedbackSuppressed)
                return;

            orig_Initialize(judge, msec, isBreak);
        }
    }
}
