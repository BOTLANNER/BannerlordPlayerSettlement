
using System;

using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using Helpers;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(BuildingsCampaignBehavior))]
    public static class BuildingsCampaignBehaviorPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(DailyTickSettlement))]
        public static bool DailyTickSettlement(ref BuildingsCampaignBehavior __instance, Settlement settlement)
        {
            if (settlement.IsFortification)
            {
                Town town = settlement.Town;
                if (town.Owner.Settlement.OwnerClan != Clan.PlayerClan)
                {
                    return true;
                }

                if (!town.CurrentBuilding.BuildingType.IsDailyProject && (settlement.IsPlayerBuilt() || settlement.IsOverwritten(out _)))
                {
                    try
                    {
                        TickCurrentBuildingForTown(town);
                    }
                    catch (Exception e)
                    {
                        LogManager.Log.NotifyBad(e);
                    }
                    return false;
                }
            }

            return true;
        }

        private static void TickCurrentBuildingForTown(Town town)
        {
            if (town.BuildingsInProgress.Peek().CurrentLevel == 3)
            {
                town.BuildingsInProgress.Dequeue();
            }
            if (!town.Owner.Settlement.IsUnderSiege && !town.BuildingsInProgress.IsEmpty<Building>())
            {
                BuildingConstructionModel buildingConstructionModel = Campaign.Current.Models.BuildingConstructionModel;
                Building construction = town.BuildingsInProgress.Peek();
                construction.BuildingProgress += town.Construction;
                int num = (town.IsCastle ? buildingConstructionModel.CastleBoostCost : buildingConstructionModel.TownBoostCost);
                if (town.BoostBuildingProcess > 0)
                {
                    town.BoostBuildingProcess -= num;
                    if (town.BoostBuildingProcess < 0)
                    {
                        town.BoostBuildingProcess = 0;
                    }
                }
                BuildingHelper.CheckIfBuildingIsComplete(construction);
            }
        }
    }
}
