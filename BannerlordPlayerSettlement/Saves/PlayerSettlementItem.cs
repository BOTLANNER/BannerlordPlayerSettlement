
using System;
using System.Collections.Generic;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace BannerlordPlayerSettlement.Saves
{
    public class PlayerSettlementItem
    {
        [SaveableField(112)]
        public string? ItemXML = null;

        [SaveableField(113)]
        public Settlement? Settlement = null;

        [System.Obsolete("Replaced with `Identifier`")]
        [SaveableField(114)]
        public string ItemIdentifier = null; // "player_settlement_town_village_1";

        [SaveableField(115)]
        public string SettlementName = "{=player_settlement_n_01}Player Settlement";

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

                int duration = Main.Settings.BuildDurationDays;
                if (Type == ((int)SettlementType.Castle))
                {
                    duration = Main.Settings.BuildCastleDurationDays;
                }
                else if (Type == ((int)SettlementType.Village))
                {
                    duration = Main.Settings.BuildVillageDurationDays;
                }

                CampaignTime buildEnd = buildStart + CampaignTime.Days(duration);
                return buildEnd;
            }
        }

        [SaveableField(201)]
        public List<PlayerSettlementItem> Villages = new List<PlayerSettlementItem>();

        [System.Obsolete("Replaced with `StringId`")]
        [SaveableField(202)]
        public int Identifier = 1;

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
    }
}
