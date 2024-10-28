using Bannerlord.ButterLib.HotKeys;

using TaleWorlds.InputSystem;

namespace BannerlordPlayerSettlement.Extensions
{
    public static class HotKeyBaseExtensions 
    {
        public static InputKey GetInputKey(this HotKeyBase hotKeyBase)
        {
            GameKey gameKey = hotKeyBase;

            return gameKey.KeyboardKey.InputKey;
        }
    }
}
