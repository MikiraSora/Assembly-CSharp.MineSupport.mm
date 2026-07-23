#pragma warning disable CS0626
using MineSupport;

namespace Manager
{
    public static class patch_GameManager
    {
        public static extern bool orig_IsAutoPlay();

        public static bool IsAutoPlay()
        {
            if (MineRuntime.SuppressAutoPlay)
                return false;

            return orig_IsAutoPlay();
        }
    }
}
