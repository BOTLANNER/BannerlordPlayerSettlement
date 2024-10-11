using System;

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace BannerlordPlayerSettlement.UI.Extensions
{

    //// Base game | Better Time
    [PrefabExtension("MapBar", "/Prefab/Window/Widget/Children")]
    //// Weather Indicators | Better Time
    [PrefabExtension("NewAncientMapBar", "/Prefab/Window/Widget/Children")]
    [PrefabExtension("NewMapBar", "/Prefab/Window/Widget/Children")]

    public class MapBarExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Child;

        [PrefabExtensionFileName]
        public String GetPrefabExtension => "PlayerSettlementInfoWidget.xml";
    }


}
