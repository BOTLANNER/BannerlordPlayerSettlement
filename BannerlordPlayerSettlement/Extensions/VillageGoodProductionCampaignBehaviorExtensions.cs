using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Helpers;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace BannerlordPlayerSettlement.Extensions
{
    public static class VillageGoodProductionCampaignBehaviorExtensions
    {
        static FastInvokeHandler TickProductionsMethod = MethodInvoker.GetHandler(AccessTools.Method(typeof(VillageGoodProductionCampaignBehavior), "TickProductions"));

        static FastInvokeHandler SetTradeBoundMethod = MethodInvoker.GetHandler(AccessTools.Property(typeof(Village), "TradeBound").SetMethod);

        public static void SetTradeBound(this Village village, Settlement? tradeBound)
        {
            SetTradeBoundMethod.Invoke(village, tradeBound);
        }

        public static void NewVillageBuilt(this VillageGoodProductionCampaignBehavior villageGoodProductionCampaignBehavior, Village village)
        {
            bool initialProductionForTowns = true;
            TickProductionsMethod.Invoke(villageGoodProductionCampaignBehavior, village.Settlement, initialProductionForTowns);

            TryToAssignTradeBoundForVillage(village);
        }

        private static void TryToAssignTradeBoundForVillage(Village village)
        {
            Settlement settlement = SettlementHelper.FindNearestSettlement((Settlement x) =>
            {
                if (!x.IsTown)
                {
                    return false;
                }
                return x.Town.MapFaction == village.Settlement.MapFaction;
            }, village.Settlement);
            if (settlement != null && Campaign.Current.Models.MapDistanceModel.GetDistance(settlement, village.Settlement) < 150f)
            {
                village.SetTradeBound(settlement);
                return;
            }
            Settlement settlement1 = SettlementHelper.FindNearestSettlement((Settlement x) =>
            {
                if (!x.IsTown || x.Town.MapFaction == village.Settlement.MapFaction || x.Town.MapFaction.IsAtWarWith(village.Settlement.MapFaction))
                {
                    return false;
                }
                return Campaign.Current.Models.MapDistanceModel.GetDistance(x, village.Settlement) <= 150f;
            }, village.Settlement);
            if (settlement1 != null && Campaign.Current.Models.MapDistanceModel.GetDistance(settlement1, village.Settlement) < 150f)
            {
                village.SetTradeBound(settlement);
                return;
            }
            village.SetTradeBound(null);
        }
    }
}
