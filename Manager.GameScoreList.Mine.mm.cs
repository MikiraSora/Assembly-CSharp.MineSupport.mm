#pragma warning disable CS0626
using MineSupport;
using MonoMod;

namespace Manager
{
    public class patch_GameScoreList : GameScoreList
    {
        [MonoModIgnore]
        public patch_GameScoreList(int index) : base(index)
        {
        }

        public extern void orig_Initialize(int monitorIndex, bool isParty);

        public void Initialize(int monitorIndex, bool isParty)
        {
            MineRuntime.ResetScore(this);
            orig_Initialize(monitorIndex, isParty);
        }

        public extern void orig_SetResult(int index, NoteScore.EScoreType kind, NoteJudge.ETiming timing);

        public void SetResult(int index, NoteScore.EScoreType kind, NoteJudge.ETiming timing)
        {
            if (MineRuntime.IsMineScore(this, index))
            {
                var originalTiming = timing;
                timing = MineRuntime.ConvertMineResult(timing);
                PatchLog.WriteLine($"[Mine] score index={index}, kind={kind}, original={originalTiming}, mapped={timing}");
            }

            orig_SetResult(index, kind, timing);
        }
    }
}
