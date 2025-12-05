
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Saves;
using BannerlordPlayerSettlement.UI.Viewmodels;
using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using SandBox.View.Map;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(SettlementVisualManager))]
    public static class SettlementVisualManagerPatch
    {
        static MethodInfo GetDesiredDecalColorMethod = AccessTools.Method(typeof(SettlementVisualManager), "GetDesiredDecalColor");

        public static uint GetDesiredDecalColor(this SettlementVisualManager instance, bool isHovered, bool isEnemy, bool isEmpty, bool isPlayerLeader)
        {
            return (uint) GetDesiredDecalColorMethod.Invoke(instance, new object[] { isHovered, isEnemy, isEmpty, isPlayerLeader });
        }


        static MethodInfo GetDesiredMaterialNameMethod = AccessTools.Method(typeof(SettlementVisualManager), "GetDesiredMaterialName");

        public static string GetDesiredMaterialName(this SettlementVisualManager instance, bool isRanged, bool isAttacker, bool isTower)
        {
            return (string) GetDesiredMaterialNameMethod.Invoke(instance, new object[] { isRanged, isAttacker, isTower });
        }


        // TODO: Repatch?
        [HarmonyPrefix]
        [HarmonyPatch(nameof(TickSiegeMachineCircles))]
        public static bool TickSiegeMachineCircles(ref SettlementVisualManager __instance, ref UIntPtr ____hoveredSiegeEntityID, ref GameEntity[] ____defenderMachinesCircleEntities, ref GameEntity[] ____attackerRangedMachinesCircleEntities, ref GameEntity[] ____attackerRamMachinesCircleEntities, ref GameEntity[] ____attackerTowerMachinesCircleEntities)
        {
            try
            {
                MatrixFrame globalFrame;
                string name;
                bool flag;
                string str;
                bool flag1;
                string name1;
                bool flag2;
                string str1;
                bool flag3;
                SiegeEvent playerSiegeEvent = PlayerSiege.PlayerSiegeEvent;
                bool flag4 = (playerSiegeEvent == null || !playerSiegeEvent.IsPlayerSiegeEvent ? false : Campaign.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(playerSiegeEvent, PlayerSiege.PlayerSide) == Hero.MainHero);
                bool isPreparationComplete = playerSiegeEvent?.BesiegerCamp?.IsPreparationComplete ?? false;
                Settlement? besiegedSettlement = playerSiegeEvent?.BesiegedSettlement;

                if (!(besiegedSettlement.IsPlayerBuilt() || besiegedSettlement.IsOverwritten(out OverwriteSettlementItem overwriteSettlementItem)))
                {
                    return true;
                }

                try
                {
                    SiegeEvent.SiegeEnginesContainer? defenderSiegeEngines = playerSiegeEvent!.GetSiegeEventSide(BattleSideEnum.Defender)?.SiegeEngines;
                    //var defenderSiegeEngineConstructions = defenderSiegeEngines?.DeployedSiegeEngines;
                    //if (defenderSiegeEngineConstructions != null && defenderSiegeEngineConstructions.Count < 4)
                    //{
                    //    defenderSiegeEngineConstructions.AddRange(Enumerable.Repeat<SiegeEngineConstructionProgress?>(null, 4 - defenderSiegeEngineConstructions.Count));
                    //}
                    var defenderDeployedRangedSiegeEngines = defenderSiegeEngines?.DeployedRangedSiegeEngines;
                    if (defenderDeployedRangedSiegeEngines != null && defenderDeployedRangedSiegeEngines.Length < 4)
                    {
                        Array.Resize(ref defenderDeployedRangedSiegeEngines, 4);
                    }

                    SiegeEvent.SiegeEnginesContainer? attackerSiegeEngines = playerSiegeEvent!.GetSiegeEventSide(BattleSideEnum.Attacker)?.SiegeEngines;
                    //var attackerSiegeEngineConstructions = attackerSiegeEngines?.DeployedSiegeEngines;
                    //if (attackerSiegeEngineConstructions != null && attackerSiegeEngineConstructions.Count < 4)
                    //{
                    //    attackerSiegeEngineConstructions.AddRange(Enumerable.Repeat<SiegeEngineConstructionProgress?>(null, 4 - attackerSiegeEngineConstructions.Count));
                    //}
                    var attackerDeployedRangedSiegeEngines = attackerSiegeEngines?.DeployedRangedSiegeEngines;
                    if (defenderDeployedRangedSiegeEngines != null && defenderDeployedRangedSiegeEngines.Length < 4)
                    {
                        Array.Resize(ref defenderDeployedRangedSiegeEngines, 4);
                    }
                    var attackerDeployedMeleeSiegeEngines = attackerSiegeEngines?.DeployedMeleeSiegeEngines;
                    if (attackerDeployedMeleeSiegeEngines != null && attackerDeployedMeleeSiegeEngines.Length < 3)
                    {
                        Array.Resize(ref attackerDeployedMeleeSiegeEngines, 3);
                    }
                }
                catch (Exception e)
                {
                    LogManager.EventTracer.Trace(new List<string> { e.Message, e.StackTrace });
                }

                SettlementVisual settlementVisual = __instance.GetSettlementVisual(playerSiegeEvent.BesiegedSettlement);
                Tuple<MatrixFrame, SettlementVisual> item = null;
                if (____hoveredSiegeEntityID != UIntPtr.Zero)
                {
                    item = MapScreenPatch.FrameAndVisualOfEngines()[____hoveredSiegeEntityID];
                }
                for (int i = 0; i < (int) settlementVisual.GetDefenderRangedSiegeEngineFrames().Length; i++)
                {
                    if (i >= playerSiegeEvent!.GetSiegeEventSide(BattleSideEnum.Defender).SiegeEngines.DeployedRangedSiegeEngines.Length)
                    {
                        continue;
                    }
                    bool deployedRangedSiegeEngines = playerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Defender).SiegeEngines.DeployedRangedSiegeEngines[i] == null;
                    bool playerSide = PlayerSiege.PlayerSide != BattleSideEnum.Defender;
                    string desiredMaterialName = __instance.GetDesiredMaterialName(true, false, false);
                    Decal componentAtIndex = ____defenderMachinesCircleEntities[i].GetComponentAtIndex(0, GameEntity.ComponentType.Decal) as Decal;
                    Material material = componentAtIndex.GetMaterial();
                    if (material != null)
                    {
                        name = material.Name;
                    }
                    else
                    {
                        name = null;
                    }
                    if (name != desiredMaterialName)
                    {
                        componentAtIndex.SetMaterial(Material.GetFromResource(desiredMaterialName));
                    }
                    if (item == null)
                    {
                        flag = false;
                    }
                    else
                    {
                        globalFrame = ____defenderMachinesCircleEntities[i].GetGlobalFrame();
                        flag = globalFrame.NearlyEquals(item.Item1, 1E-05f);
                    }
                    uint desiredDecalColor = __instance.GetDesiredDecalColor(flag, playerSide, deployedRangedSiegeEngines, flag4);
                    if (desiredDecalColor != componentAtIndex.GetFactor1())
                    {
                        componentAtIndex.SetFactor1(desiredDecalColor);
                    }
                }
                for (int j = 0; j < (int) settlementVisual.GetAttackerRangedSiegeEngineFrames().Length; j++)
                {
                    if (j >= playerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker).SiegeEngines.DeployedRangedSiegeEngines.Length)
                    {
                        continue;
                    }
                    bool deployedRangedSiegeEngines1 = playerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker).SiegeEngines.DeployedRangedSiegeEngines[j] == null;
                    bool playerSide1 = PlayerSiege.PlayerSide != BattleSideEnum.Attacker;
                    string desiredMaterialName1 = __instance.GetDesiredMaterialName(true, true, false);
                    Decal decal = ____attackerRangedMachinesCircleEntities[j].GetComponentAtIndex(0, GameEntity.ComponentType.Decal) as Decal;
                    Material material1 = decal.GetMaterial();
                    if (material1 != null)
                    {
                        str = material1.Name;
                    }
                    else
                    {
                        str = null;
                    }
                    if (str != desiredMaterialName1)
                    {
                        decal.SetMaterial(Material.GetFromResource(desiredMaterialName1));
                    }
                    if (item == null)
                    {
                        flag1 = false;
                    }
                    else
                    {
                        globalFrame = ____attackerRangedMachinesCircleEntities[j].GetGlobalFrame();
                        flag1 = globalFrame.NearlyEquals(item.Item1, 1E-05f);
                    }
                    uint num = __instance.GetDesiredDecalColor(flag1, playerSide1, deployedRangedSiegeEngines1, flag4);
                    if (num != decal.GetFactor1())
                    {
                        decal.SetFactor1(num);
                    }
                }
                for (int k = 0; k < (int) settlementVisual.GetAttackerBatteringRamSiegeEngineFrames().Length; k++)
                {
                    if (k >= playerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker).SiegeEngines.DeployedMeleeSiegeEngines.Length)
                    {
                        continue;
                    }

                    bool deployedMeleeSiegeEngines = playerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker).SiegeEngines.DeployedMeleeSiegeEngines[k] == null;
                    bool playerSide2 = PlayerSiege.PlayerSide != BattleSideEnum.Attacker;
                    string desiredMaterialName2 = __instance.GetDesiredMaterialName(false, true, false);
                    Decal componentAtIndex1 = ____attackerRamMachinesCircleEntities[k].GetComponentAtIndex(0, GameEntity.ComponentType.Decal) as Decal;
                    Material material2 = componentAtIndex1.GetMaterial();
                    if (material2 != null)
                    {
                        name1 = material2.Name;
                    }
                    else
                    {
                        name1 = null;
                    }
                    if (name1 != desiredMaterialName2)
                    {
                        componentAtIndex1.SetMaterial(Material.GetFromResource(desiredMaterialName2));
                    }
                    if (item == null)
                    {
                        flag2 = false;
                    }
                    else
                    {
                        globalFrame = ____attackerRamMachinesCircleEntities[k].GetGlobalFrame();
                        flag2 = globalFrame.NearlyEquals(item.Item1, 1E-05f);
                    }
                    uint desiredDecalColor1 = __instance.GetDesiredDecalColor(flag2, playerSide2, deployedMeleeSiegeEngines, flag4);
                    if (desiredDecalColor1 != componentAtIndex1.GetFactor1())
                    {
                        componentAtIndex1.SetFactor1(desiredDecalColor1);
                    }
                }
                for (int l = 0; l < (int) settlementVisual.GetAttackerTowerSiegeEngineFrames().Length; l++)
                {
                    if (((int) settlementVisual.GetAttackerBatteringRamSiegeEngineFrames().Length + l) >= playerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker).SiegeEngines.DeployedMeleeSiegeEngines.Length)
                    {
                        continue;
                    }

                    bool deployedMeleeSiegeEngines1 = playerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker).SiegeEngines.DeployedMeleeSiegeEngines[(int) settlementVisual.GetAttackerBatteringRamSiegeEngineFrames().Length + l] == null;
                    bool playerSide3 = PlayerSiege.PlayerSide != BattleSideEnum.Attacker;
                    string str2 = __instance.GetDesiredMaterialName(false, true, true);
                    Decal decal1 = ____attackerTowerMachinesCircleEntities[l].GetComponentAtIndex(0, GameEntity.ComponentType.Decal) as Decal;
                    Material material3 = decal1.GetMaterial();
                    if (material3 != null)
                    {
                        str1 = material3.Name;
                    }
                    else
                    {
                        str1 = null;
                    }
                    if (str1 != str2)
                    {
                        decal1.SetMaterial(Material.GetFromResource(str2));
                    }
                    if (item == null)
                    {
                        flag3 = false;
                    }
                    else
                    {
                        globalFrame = ____attackerTowerMachinesCircleEntities[l].GetGlobalFrame();
                        flag3 = globalFrame.NearlyEquals(item.Item1, 1E-05f);
                    }
                    uint num1 = __instance.GetDesiredDecalColor(flag3, playerSide3, deployedMeleeSiegeEngines1, flag4);
                    if (num1 != decal1.GetFactor1())
                    {
                        decal1.SetFactor1(num1);
                    }
                }


                return false;
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }

            return true;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(TickSiegeMachineCircles))]
        public static Exception? FixTickSiegeMachineCircles(Exception? __exception, ref MapScreen __instance)
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