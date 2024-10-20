﻿using System;

using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(SiegeEnginesContainer))]
    public static class SiegeEnginesContainerPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(SiegeEnginesContainer.DeploySiegeEngineAtIndex))]
        public static void DeploySiegeEngineAtIndex(ref SiegeEnginesContainer __instance, SiegeEvent.SiegeEngineConstructionProgress siegeEngine, int index)
        {
            try
            {
                SiegeEvent.SiegeEngineConstructionProgress[] siegeEngineConstructionProgressArray;
                siegeEngineConstructionProgressArray = (siegeEngine.SiegeEngine.IsRanged ? __instance.DeployedRangedSiegeEngines : __instance.DeployedMeleeSiegeEngines);

                if (index >= siegeEngineConstructionProgressArray.Length)
                {
                    Array.Resize(ref siegeEngineConstructionProgressArray, index + 1);
                }

            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }
        }
        [HarmonyPrefix]
        [HarmonyPatch(nameof(SiegeEnginesContainer.RemoveDeployedSiegeEngine))]
        public static void RemoveDeployedSiegeEngine(ref SiegeEnginesContainer __instance, int index, bool isRanged, bool moveToReserve)
        {
            try
            {
                SiegeEvent.SiegeEngineConstructionProgress[] siegeEngineConstructionProgressArray = (isRanged ? __instance.DeployedRangedSiegeEngines : __instance.DeployedMeleeSiegeEngines);

                if (index >= siegeEngineConstructionProgressArray.Length)
                {
                    Array.Resize(ref siegeEngineConstructionProgressArray, index + 1);
                }

            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }
        }

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(SiegeEnginesContainer.DeploySiegeEngineAtIndex))]
        public static Exception? FixDeploySiegeEngineAtIndex(ref Exception __exception, ref SiegeEnginesContainer __instance, SiegeEvent.SiegeEngineConstructionProgress siegeEngine, int index)
        {
            if (__exception != null)
            {
                var e = __exception;
                LogManager.Log.NotifyBad(e);
            }
            return null;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(SiegeEnginesContainer.RemoveDeployedSiegeEngine))]
        public static Exception? FixRemoveDeployedSiegeEngine(ref Exception __exception, ref SiegeEnginesContainer __instance, int index, bool isRanged, bool moveToReserve)
        {
            if (__exception != null)
            {
                var e = __exception;
                LogManager.Log.NotifyBad(e);
            }
            return null;
        }
    }
}
