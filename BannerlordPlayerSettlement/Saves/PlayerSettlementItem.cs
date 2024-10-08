
using System.Collections.Generic;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
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
                CampaignTime buildEnd = buildStart + CampaignTime.Days(Main.Settings.BuildDurationDays);
                return buildEnd;
            }
        }

        [SaveableField(201)]
        public List<PlayerSettlementItem> Villages = new List<PlayerSettlementItem>();

        [SaveableField(202)]
        public int Identifier = 1;

        [SaveableField(204)]
        public int Type = 0;

        [SaveableField(206)]
        public Mat3Saveable? RotationMat3 = null;

        public static string? GetStringIdFor(SettlementType settlementType, int id, PlayerSettlementItem? boundTarget = null)
        {
            switch (settlementType)
            {
                default:
                case SettlementType.None:
                    return null;
                case SettlementType.Town:
                    return $"player_settlement_town_{id}";
                case SettlementType.Village:
                    if (boundTarget == null)
                    {
                        return null;
                    }
                    if (boundTarget.Type == (int) SettlementType.Castle)
                    {
                        return $"player_settlement_castle_{boundTarget.Identifier}_village_{id}";
                    }
                    return $"player_settlement_town_{boundTarget.Identifier}_village_{id}";
                case SettlementType.Castle:
                    return $"player_settlement_castle_{id}";
            }
        }

        public string? GetStringId(SettlementType settlementType, PlayerSettlementItem? boundTarget = null)
        {
            return GetStringIdFor(settlementType, Identifier, boundTarget);
        }
    }
}
