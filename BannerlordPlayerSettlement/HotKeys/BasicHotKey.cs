
using Bannerlord.ButterLib.HotKeys;

using TaleWorlds.InputSystem;

namespace BannerlordPlayerSettlement.HotKeys
{
    public class BasicHotKey : HotKeyBase
    {
        public BasicHotKey(string displayName, string description, InputKey defaultKey, string category, string uniqueness) : base(nameof(BasicHotKey) + defaultKey.ToString() + uniqueness, displayName, description, defaultKey, category)
        {
        }
    }
}
