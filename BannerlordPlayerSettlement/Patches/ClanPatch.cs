﻿
//using BannerlordPlayerSettlement.Behaviours;
//using BannerlordPlayerSettlement.Extensions;

//using HarmonyLib;

//using TaleWorlds.CampaignSystem;
//using TaleWorlds.CampaignSystem.GameComponents;
//using TaleWorlds.CampaignSystem.Settlements;
//using TaleWorlds.Library;

//namespace BannerlordPlayerSettlement.Patches
//{
//    [HarmonyPatch(typeof(Clan))]
//    public static class ClanPatch
//    {
//        [HarmonyPrefix]
//        [HarmonyPatch(nameof(FindSettlementScoreForBeingHomeSettlement))]
//        public static bool FindSettlementScoreForBeingHomeSettlement(ref float __result, ref Clan __instance, Settlement settlement)
//        {
//            try
//            {
//                if (settlement.IsPlayerBuilt())
//                {
//                    //__result = 0f;
//                    //return false;
//                    return true;
//                }
//            }
//            catch (System.Exception e) { 
//                LogManager.Log.NotifyBad(e);
//            }
//            return true;
//        }
//    }
//}
