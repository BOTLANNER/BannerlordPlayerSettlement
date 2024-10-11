using System;

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace BannerlordPlayerSettlement.UI.Extensions
{
    //// Base game
    [PrefabExtension("MapBar", "descendant::VisualDefinitions")]
    //// Weather Indicators
    [PrefabExtension("NewAncientMapBar", "descendant::VisualDefinitions")]
    [PrefabExtension("NewMapBar", "descendant::VisualDefinitions")]
    public class MapBarVisualDefnitionExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Child;

        [PrefabExtensionFileName]
        public String GetPrefabExtension => "PlayerSettlementInfoWidgetVisualDefinition.xml";
    }


}
