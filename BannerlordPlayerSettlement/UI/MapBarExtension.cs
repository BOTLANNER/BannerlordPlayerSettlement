using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace BannerlordPlayerSettlement.UI
{
    [PrefabExtension("MapBar", "(descendant::MapCurrentTimeVisualWidget | descendant::TimePanel)[1]")]

    public class MapBarExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Prepend;

        [PrefabExtensionFileName]
        public String GetPrefabExtension => "CreatePlayerSettlementMapButton.xml";
    }
}
