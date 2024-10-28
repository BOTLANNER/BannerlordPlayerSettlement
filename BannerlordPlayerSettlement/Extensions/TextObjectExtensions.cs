using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TaleWorlds.Localization;

namespace BannerlordPlayerSettlement.Extensions
{
    public static class TextObjectExtensions
    {
        public static TextObject AsEmpty(this TextObject? t)
        {
            if (t != null)
            {
                t.Value = "";
                t.Attributes?.Clear();
                return t;
            }

            return TextObjectCompat.Empty;
        }
    }

    public static class TextObjectCompat
    {
        public static readonly TextObject Empty = new TextObject();
    }
}
