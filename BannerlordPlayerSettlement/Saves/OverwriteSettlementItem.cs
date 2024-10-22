
using System;
using System.Collections.Generic;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace BannerlordPlayerSettlement.Saves
{
    public class OverwriteSettlementItem : ISettlementItem
    {
        [SaveableField(112)]
        public string? ItemXML = null;

        [SaveableField(113)]
        public Settlement? Settlement = null;

        [SaveableField(115)]
        public string SettlementName = null;

        [SaveableField(116)]
        public float BuiltAt = -1f;

        [SaveableField(117)]
        public bool BuildComplete = false;

        public CampaignTime BuildEnd
        {
            get
            {
                if (Main.Settings == null || !Main.Settings.Enabled)
                {
                    return CampaignTime.Never;
                }

                if (Main.Settings.InstantBuild)
                {
                    return CampaignTime.Now;
                }

                CampaignTime buildStart = CampaignTime.Hours(BuiltAt - 5);

                int duration = Main.Settings.RebuildTownDurationDays;
                if (Type == ((int)SettlementType.Castle))
                {
                    duration = Main.Settings.RebuildCastleDurationDays;
                }
                else if (Type == ((int)SettlementType.Village))
                {
                    duration = Main.Settings.RebuildVillageDurationDays;
                }

                CampaignTime buildEnd = buildStart + CampaignTime.Days(duration);
                return buildEnd;
            }
        }

        [SaveableField(204)]
        public int Type = 0;

        [SaveableField(206)]
        public Mat3Saveable? RotationMat3 = null;

        [SaveableField(207)]
        public string Version = null;

        [SaveableField(208)]
        public string StringId = null;

        [SaveableField(209)]
        public string PrefabId = null;

        [SaveableField(210)]
        public List<DeepTransformEdit>? DeepEdits = new();

        public SettlementType GetSettlementType()
        {
            return (SettlementType) Type;
        }

        public static string EncyclopediaLink(string StringId) => String.Concat(Campaign.Current.EncyclopediaManager.GetIdentifier(typeof(Settlement)), "-", StringId) ?? "";

        public static TextObject EncyclopediaLinkWithName(string StringId, TextObject Name) => HyperlinkTexts.GetSettlementHyperlinkText(EncyclopediaLink(StringId), Name);

        public void SetBuildComplete(bool completed)
        {
            BuildComplete = completed;
        }

        public string? GetSettlementName()
        {
            return SettlementName;
        }

        public Settlement? GetSettlement()
        {
            return Settlement;
        }
    }
}
