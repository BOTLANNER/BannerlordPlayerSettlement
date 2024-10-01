using System;
using System.Collections.Generic;
using System.Linq;

using BannerlordPlayerSettlement.Extensions;

using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace BannerlordPlayerSettlement.Saves
{
    public class PlayerSettlementInfo
    {
        static Color Error = new(178 * 255, 34 * 255, 34 * 255);

        [SaveableField(102)]
        public string? PlayerSettlementXML = null;

        [SaveableField(103)]
        public Settlement? PlayerSettlement = null;

        private static PlayerSettlementInfo? _instance = null;
        public static PlayerSettlementInfo? Instance
        {
            get
            {
                return _instance;
            }
            set
            {
                _instance = value;
            }
        }

        [SaveableField(104)]
        public string PlayerSettlementIdentifier = "player_settlement_town_01";

        [SaveableField(105)]
        public string PlayerSettlementName = "{=player_settlement_n_01}Player Settlement";

        [SaveableField(106)]
        public float BuiltAt = -1f;

        [SaveableField(107)]
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

        public PlayerSettlementInfo()
        {
        }


        internal void OnLoad()
        {
            try
            {
                if (PlayerSettlement != null && PlayerSettlement.Town != null)
                {
                    var campaignGameStarter = SandBoxManager.Instance.GameStarter;
                    var craftingCampaignBehavior = campaignGameStarter.CampaignBehaviors.FirstOrDefault(b => b is CraftingCampaignBehavior) as CraftingCampaignBehavior;
                    if (craftingCampaignBehavior != null && craftingCampaignBehavior.CraftingOrders != null)
                    {
                        craftingCampaignBehavior.AddTown(PlayerSettlement.Town, out _);
                        //craftingCampaignBehavior?.CraftingOrders?.AddItem(new KeyValuePair<Town, CraftingCampaignBehavior.CraftingOrderSlots>(PlayerSettlement.Town, new CraftingCampaignBehavior.CraftingOrderSlots()));
                    }

                }
            }
            catch (Exception e)
            {
                Debug.PrintError(e.Message, e.StackTrace);
                Debug.WriteDebugLineOnScreen(e.ToString());
                Debug.SetCrashReportCustomString(e.Message);
                Debug.SetCrashReportCustomStack(e.StackTrace);
                InformationManager.DisplayMessage(new InformationMessage(e.ToString(), Error));
            }
        }
    }
}
