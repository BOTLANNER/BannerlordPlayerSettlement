
using System;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Extensions;

using HarmonyLib;

using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(DefaultMapDistanceModel))]
    public static class DefaultMapDistanceModelPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(GetDistance), new Type[] { typeof(Settlement), typeof(Settlement) })]
        public static bool GetDistance(ref float __result, ref DefaultMapDistanceModel __instance, Settlement fromSettlement, Settlement toSettlement)
        {
            try
            {
                if (fromSettlement.IsPlayerBuilt())
                {
                    __result = float.MaxValue;
                    return false;
                }
                if (toSettlement.IsPlayerBuilt())
                {
                    __result = float.MaxValue;
                    return false;
                }
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(GetDistance), new Type[] { typeof(MobileParty), typeof(Settlement) })]
        public static bool GetDistance(ref float __result, ref DefaultMapDistanceModel __instance, MobileParty fromParty, Settlement toSettlement)
        {
            try
            {
                if (fromParty != null && fromParty.Party != null && fromParty.Party.Settlement.IsPlayerBuilt())
                {
                    __result = float.MaxValue;
                    return false;
                }
                if (toSettlement.IsPlayerBuilt())
                {
                    __result = float.MaxValue;
                    return false;
                }
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(GetDistance), new Type[] { typeof(MobileParty), typeof(MobileParty) })]
        public static bool GetDistance(ref float __result, ref DefaultMapDistanceModel __instance, MobileParty fromParty, MobileParty toParty)
        {
            try
            {
                if (fromParty != null && fromParty.Party != null && fromParty.Party.Settlement != null && fromParty.Party.Settlement.IsPlayerBuilt())
                {
                    __result = float.MaxValue;
                    return false;
                }
                if (toParty != null && toParty.Party != null && toParty.Party.Settlement != null && toParty.Party.Settlement.IsPlayerBuilt())
                {
                    __result = float.MaxValue;
                    return false;
                }
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
            return true;
        }


        [HarmonyPrefix]
        [HarmonyPatch(nameof(GetDistance), new Type[] { typeof(Settlement), typeof(Settlement), typeof(float), typeof(float) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
        
        public static bool GetDistance(ref bool __result, ref DefaultMapDistanceModel __instance, Settlement fromSettlement, Settlement toSettlement, float maximumDistance, ref float distance)
        {
            try
            {
                if (fromSettlement != null && fromSettlement.IsPlayerBuilt())
                {
                    distance = float.MaxValue;
                    __result = false;
                    return false;
                }
                if (toSettlement != null && toSettlement.IsPlayerBuilt())
                {
                    distance = float.MaxValue;
                    __result = false;
                    return false;
                }
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(GetDistance), new Type[] { typeof(MobileParty), typeof(Settlement), typeof(float), typeof(float) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
        
        public static bool GetDistance(ref bool __result, ref DefaultMapDistanceModel __instance, MobileParty fromParty, Settlement toSettlement, float maximumDistance, ref float distance)
        {
            try
            {
                if (fromParty != null && fromParty.Party != null && fromParty.Party.Settlement != null && fromParty.Party.Settlement.IsPlayerBuilt())
                {
                    distance = float.MaxValue;
                    __result = false;
                    return false;
                }
                if (toSettlement != null && toSettlement.IsPlayerBuilt())
                {
                    distance = float.MaxValue;
                    __result = false;
                    return false;
                }
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(GetDistance), new Type[] { typeof(IMapPoint), typeof(MobileParty), typeof(float), typeof(float) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
        
        public static bool GetDistance(ref bool __result, ref DefaultMapDistanceModel __instance, IMapPoint fromMapPoint, MobileParty toParty, float maximumDistance, ref float distance)
        {
            try
            {
                if (toParty != null && toParty.Party != null && toParty.Party.Settlement != null && toParty.Party.Settlement.IsPlayerBuilt())
                {
                    distance = float.MaxValue;
                    __result = false;
                    return false;
                }
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(GetDistance), new Type[] { typeof(IMapPoint), typeof(Settlement), typeof(float), typeof(float) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
        
        public static bool GetDistance(ref bool __result, ref DefaultMapDistanceModel __instance, IMapPoint fromMapPoint, Settlement toSettlement, float maximumDistance, ref float distance)
        {
            try
            {
                if (toSettlement != null && toSettlement.IsPlayerBuilt())
                {
                    distance = float.MaxValue;
                    __result = false;
                    return false;
                }
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
            return true;
        }
    }
}
