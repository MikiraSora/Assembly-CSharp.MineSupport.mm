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
            MineRuntime.RegisterScore(this, monitorIndex);
        }

        public extern void orig_InitializeHideScore(int monitorIndex, bool isParty);

        public void InitializeHideScore(int monitorIndex, bool isParty)
        {
            MineRuntime.ResetScore(this);
            orig_InitializeHideScore(monitorIndex, isParty);
        }

        public extern void orig_SetResult(int index, NoteScore.EScoreType kind, NoteJudge.ETiming timing);

        public void SetResult(int index, NoteScore.EScoreType kind, NoteJudge.ETiming timing)
        {
            NoteJudge.ETiming mappedTiming;
            var converted = MineRuntime.TryConvertResult(this, index, timing, out mappedTiming);
            timing = mappedTiming;

            orig_SetResult(index, kind, timing);
            MineRuntime.RecordResult(this, index, kind, converted);
        }

        public extern void orig_FinishPlay(GameScoreList otherSideScore);

        public void FinishPlay(GameScoreList otherSideScore)
        {
            var scope = MineRuntime.EnterNaturalFinish(this);
            try
            {
                orig_FinishPlay(otherSideScore);
            }
            finally
            {
                MineRuntime.Exit(scope);
            }
        }
    }
}
