#pragma warning disable CS0626
using Manager;
using MineSupport;

namespace Monitor
{
    public class patch_JudgeGrade : JudgeGrade
    {
        public extern void orig_Initialize(NoteJudge.ETiming judge, float msec, NoteJudge.EJudgeType type);

        public void Initialize(NoteJudge.ETiming judge, float msec, NoteJudge.EJudgeType type)
        {
            if (MineRuntime.FeedbackSuppressed)
                return;

            orig_Initialize(judge, msec, type);
        }

        public extern void orig_InitializeBreak(NoteJudge.ETiming judge, float msec, NoteJudge.EJudgeType type);

        public void InitializeBreak(NoteJudge.ETiming judge, float msec, NoteJudge.EJudgeType type)
        {
            if (MineRuntime.FeedbackSuppressed)
                return;

            orig_InitializeBreak(judge, msec, type);
        }
    }
}
