﻿
//using BannerlordPlayerSettlement.Behaviours;
//using BannerlordPlayerSettlement.Extensions;

//using HarmonyLib;

//using TaleWorlds.CampaignSystem.CampaignBehaviors;
//using TaleWorlds.CampaignSystem.Settlements;
//using TaleWorlds.Engine;
//using TaleWorlds.Library;

//namespace BannerlordPlayerSettlement.Patches
//{
//    [HarmonyPatch(typeof(IssuesCampaignBehavior))]
//    public static class IssuesCampaignBehaviorPatch
//    {
//        [HarmonyPrefix]
//        [HarmonyPatch(nameof(CreateAnIssueForSettlementNotables))]
//        public static bool CreateAnIssueForSettlementNotables(ref bool __result, ref IssuesCampaignBehavior __instance, Settlement settlement, int totalDesiredIssueCount)
//        {
//            try
//            {
//                if (settlement.IsPlayerBuilt())
//                {
//                    __result = false;
//                    return false;
//                }
//            }
//            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }
//            return true;
//        }
//    }
//}
