using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Bannerlord.ButterLib.HotKeys;

using TaleWorlds.InputSystem;

namespace BannerlordPlayerSettlement.HotKeys
{
    public class ModifierKey : HotKeyBase
    {
        static int count = 0;
        public ModifierKey(string displayName, string description, InputKey defaultKey, string category) : base(nameof(ModifierKey) + defaultKey.ToString() + count, displayName, description, defaultKey, category)
        {
            count++;
        }
    }
}
