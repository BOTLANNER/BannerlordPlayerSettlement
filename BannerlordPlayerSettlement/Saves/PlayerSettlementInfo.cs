using System;
using System.Collections.Generic;
using System.Linq;

using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Utils;

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

        [System.Obsolete("Replaced with `Towns[0]`")]
        [SaveableField(102)]
        public string? PlayerSettlementXML = null;

        [System.Obsolete("Replaced with `Towns[0]`")]
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

        [System.Obsolete("Replaced with `Towns[0]`")]
        [SaveableField(104)]
        public string PlayerSettlementIdentifier = null;

        [System.Obsolete("Replaced with `Towns[0]`")]
        [SaveableField(105)]
        public string PlayerSettlementName = "{=player_settlement_n_01}Player Settlement";

        [System.Obsolete("Replaced with `Towns[0]`")]
        [SaveableField(106)]
        public float BuiltAt = -1f;

        [System.Obsolete("Replaced with `Towns[0]`")]
        [SaveableField(107)]
        public bool BuildComplete = false;

        //public CampaignTime BuildEnd
        //{
        //    get
        //    {
        //        if (Main.Settings == null || !Main.Settings.Enabled)
        //        {
        //            return CampaignTime.Never;
        //        }

        //        if (Main.Settings.InstantBuild)
        //        {
        //            return CampaignTime.Now;
        //        }

        //        CampaignTime buildStart = CampaignTime.Hours(BuiltAt - 5);
        //        CampaignTime buildEnd = buildStart + CampaignTime.Days(Main.Settings.BuildDurationDays);
        //        return buildEnd;
        //    }
        //}

        [System.Obsolete("Replaced with `Towns[0].Villages`")]
        [SaveableField(111)]
        public List<PlayerSettlementItem>? PlayerVillages = new List<PlayerSettlementItem>();

        // New

        [SaveableField(211)]
        public List<PlayerSettlementItem> Towns = new List<PlayerSettlementItem>();

        [SaveableField(212)]
        public List<PlayerSettlementItem> Castles = new List<PlayerSettlementItem>();

        public PlayerSettlementInfo()
        {
        }


        public void OnLoad()
        {
            try
            {
                if (Towns == null)
                {
                    Towns = new();
                }

                if (Castles == null)
                {
                    Castles = new();
                }

                if (PlayerSettlement != null && PlayerSettlement.Town != null)
                {
                    var town = new PlayerSettlementItem
                    {
                        BuildComplete = BuildComplete,
                        BuiltAt = BuiltAt,
                        Identifier = int.Parse(PlayerSettlementIdentifier.Replace("player_settlement_town_", "")),
                        Type = (int) SettlementType.Town,
                        ItemIdentifier = PlayerSettlementIdentifier, //"player_settlement_town_1",
                        ItemXML = PlayerSettlementXML, //?.Replace(PlayerSettlementIdentifier, "player_settlement_town_1"),
                        Settlement = PlayerSettlement,
                        SettlementName = PlayerSettlementName,
                        Villages = new()
                    };

                    if (PlayerVillages != null)
                    {
                        for (int i = 0; i < PlayerVillages.Count; i++)
                        {
                            var villageNumber = i + 1;
                            var v = PlayerVillages[i];
                            var village = new PlayerSettlementItem
                            {
                                BuildComplete = v.BuildComplete,
                                BuiltAt = v.BuiltAt,
                                Identifier = villageNumber,
                                Type = (int) SettlementType.Village,
                                ItemIdentifier = $"{PlayerSettlementIdentifier}_village_{villageNumber}",
                                ItemXML = v.ItemXML, //?.Replace(PlayerSettlementIdentifier, "player_settlement_town_1"),
                                Settlement = PlayerSettlement,
                                SettlementName = v.SettlementName,
                                // N/A
                                Villages = new()
                            };

                            town.Villages.Add(village);
                        }
                    }
                    Towns.Add(town);

                    PlayerSettlement = null;
                    PlayerVillages = null;
                }

                var campaignGameStarter = SandBoxManager.Instance.GameStarter;
                var craftingCampaignBehavior = campaignGameStarter.CampaignBehaviors.FirstOrDefault(b => b is CraftingCampaignBehavior) as CraftingCampaignBehavior;
                foreach (var town in Towns)
                {
                    if (craftingCampaignBehavior != null && craftingCampaignBehavior.CraftingOrders != null && town != null && town.Settlement != null && town.Settlement.Town != null)
                    {
                        craftingCampaignBehavior.AddTown(town.Settlement.Town, out _);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.PrintError(e.Message, e.StackTrace);
                Debug.WriteDebugLineOnScreen(e.ToString());
                Debug.SetCrashReportCustomString(e.Message);
                Debug.SetCrashReportCustomStack(e.StackTrace);
                InformationManager.DisplayMessage(new InformationMessage(e.ToString(), Colours.Error));
            }
        }

        public PlayerSettlementItem? FindSettlement(Settlement settlement)
        {
            if (settlement == null)
            {
                return null;
            }

            if (settlement.IsTown)
            {
                return Towns?.FirstOrDefault(t => t.Settlement == settlement);
            }
            
            if (settlement.IsCastle)
            {
                return Castles?.FirstOrDefault(c => c.Settlement == settlement);
            }

            if (settlement.IsVillage)
            {
                return Towns.SelectMany(t => t.Villages)?.FirstOrDefault(v => v.Settlement == settlement) ?? 
                    Castles.SelectMany(c => c.Villages)?.FirstOrDefault(v => v.Settlement == settlement);
            }

            return null;
        }

        public int GetVillageNumber(Settlement bound, out PlayerSettlementItem? target)
        {
            if (bound.IsTown)
            {
                target = Towns.FirstOrDefault(t => t.Settlement == bound);
                if (target == null)
                {
                    return -1;
                }
                return (target.Villages?.Count ?? 0) + 1;
            }
            else if (bound.IsCastle)
            {
                target = Castles.FirstOrDefault(t => t.Settlement == bound);
                if (target == null)
                {
                    return -1;
                }
                return (target.Villages?.Count ?? 0) + 1;
            }
            target = null;
            return -1;
        }
    }
}
