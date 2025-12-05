
using System;

using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Saves;
using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using SandBox.ViewModelCollection.MapSiege;

using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(MapSiegePOIVM))]
    public static class MapSiegePOIVMPatch
    {

        [HarmonyPrefix]
        [HarmonyPatch(nameof(GetDesiredMachine))]
        public static bool GetDesiredMachine(ref MapSiegePOIVM __instance, ref SiegeEvent.SiegeEngineConstructionProgress __result)
        {
            try
            {
                if (PlayerSiege.PlayerSiegeEvent == null)
                {
                    __result = null;
                    return true;
                }
                if (!(PlayerSiege.PlayerSiegeEvent.BesiegedSettlement.IsPlayerBuilt() || PlayerSiege.PlayerSiegeEvent.BesiegedSettlement.IsOverwritten(out OverwriteSettlementItem overwriteSettlementItem)))
                {
                    return true;
                }
                switch (__instance.Type)
                {
                    case MapSiegePOIVM.POIType.WallSection:
                        {
                            __result = null;
                            return true;
                        }
                    case MapSiegePOIVM.POIType.DefenderSiegeMachine:
                        {
                            if (__instance.MachineIndex >= PlayerSiege.PlayerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Defender).SiegeEngines.DeployedRangedSiegeEngines.Length)
                            {
                                __result = null;
                                return false;
                            }
                            __result = PlayerSiege.PlayerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Defender).SiegeEngines.DeployedRangedSiegeEngines[__instance.MachineIndex];
                            return true;
                        }
                    case MapSiegePOIVM.POIType.AttackerRamSiegeMachine:
                    case MapSiegePOIVM.POIType.AttackerTowerSiegeMachine:
                        {
                            if (__instance.MachineIndex >= PlayerSiege.PlayerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker).SiegeEngines.DeployedMeleeSiegeEngines.Length)
                            {
                                __result = null;
                                return false;
                            }
                            __result = PlayerSiege.PlayerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker).SiegeEngines.DeployedMeleeSiegeEngines[__instance.MachineIndex];
                            return true;
                        }
                    case MapSiegePOIVM.POIType.AttackerRangedSiegeMachine:
                        {
                            if (__instance.MachineIndex >= PlayerSiege.PlayerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker).SiegeEngines.DeployedRangedSiegeEngines.Length)
                            {
                                __result = null;
                                return false;
                            }
                            __result = PlayerSiege.PlayerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker).SiegeEngines.DeployedRangedSiegeEngines[__instance.MachineIndex];
                            return true;
                        }
                    default:
                        {
                            __result = null;
                            return true;
                        }
                }
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }

            return true;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(GetDesiredMachine))]
        public static Exception? FixGetDesiredMachine(Exception? __exception, ref MapSiegePOIVM __instance)
        {
            if (__exception != null)
            {
                var e = __exception;
                LogManager.Log.NotifyBad(e);
            }
            return null;
        }


        [HarmonyPrefix]
        [HarmonyPatch(nameof(RefreshHitpoints))]
        public static bool RefreshHitpoints(ref MapSiegePOIVM __instance, ref float ____bindCurrentHitpoints, ref float ____bindMaxHitpoints, ref int ____bindMachineType)
        {
            try
            {
                if (PlayerSiege.PlayerSiegeEvent == null)
                {
                    ____bindCurrentHitpoints = 0f;
                    ____bindMaxHitpoints = 0f;
                    return true;
                }
                if (!(PlayerSiege.PlayerSiegeEvent.BesiegedSettlement.IsPlayerBuilt() || PlayerSiege.PlayerSiegeEvent.BesiegedSettlement.IsOverwritten(out OverwriteSettlementItem overwriteSettlementItem)))
                {
                    return true;
                }
                MapSiegePOIVM.POIType type = __instance.Type;
                if (type != MapSiegePOIVM.POIType.WallSection)
                {
                    if ((int) type - (int) MapSiegePOIVM.POIType.DefenderSiegeMachine > (int) MapSiegePOIVM.POIType.AttackerTowerSiegeMachine)
                    {
                        return true;
                    }
                    if (__instance.Machine == null)
                    {
                        ____bindCurrentHitpoints = 0f;
                        ____bindMaxHitpoints = 0f;
                        return true;
                    }
                    if (__instance.Machine.IsActive)
                    {
                        ____bindCurrentHitpoints = __instance.Machine.Hitpoints;
                        ____bindMaxHitpoints = __instance.Machine.MaxHitPoints;
                        return true;
                    }
                    if (__instance.Machine.IsBeingRedeployed)
                    {
                        ____bindCurrentHitpoints = __instance.Machine.RedeploymentProgress;
                        ____bindMaxHitpoints = 1f;
                        return true;
                    }
                    ____bindCurrentHitpoints = __instance.Machine.Progress;
                    ____bindMaxHitpoints = 1f;
                    return true;
                }
                MBReadOnlyList<float> settlementWallSectionHitPointsRatioList = PlayerSiege.PlayerSiegeEvent.BesiegedSettlement.SettlementWallSectionHitPointsRatioList;
                ____bindMaxHitpoints = PlayerSiege.PlayerSiegeEvent.BesiegedSettlement.MaxWallHitPoints / (float) PlayerSiege.PlayerSiegeEvent.BesiegedSettlement.WallSectionCount;
                if (__instance.MachineIndex >= settlementWallSectionHitPointsRatioList.Count)
                {
                    ____bindCurrentHitpoints = 0f;
                    ____bindMachineType = 0;
                    return false;
                }
                ____bindCurrentHitpoints = settlementWallSectionHitPointsRatioList[__instance.MachineIndex] * ____bindMaxHitpoints;
                ____bindMachineType = (____bindCurrentHitpoints <= 0f ? 1 : 0);

                return false;
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }

            return true;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(RefreshHitpoints))]
        public static Exception? FixRefreshHitpoints(Exception? __exception, ref MapSiegePOIVM __instance)
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