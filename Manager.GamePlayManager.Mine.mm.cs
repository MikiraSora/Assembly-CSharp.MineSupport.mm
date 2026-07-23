#pragma warning disable CS0626
using MineSupport;

namespace Manager
{
    public class patch_GamePlayManager : GamePlayManager
    {
        public extern void orig_Initialize(bool partyPlay = false);

        public void Initialize(bool partyPlay = false)
        {
            MineRuntime.ClearTransientState();
            orig_Initialize(partyPlay);
        }
    }
}
