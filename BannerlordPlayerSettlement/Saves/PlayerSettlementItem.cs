
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

        [SaveableField(114)]
        public string ItemIdentifier = "player_settlement_town_village_1";

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
    }
}
