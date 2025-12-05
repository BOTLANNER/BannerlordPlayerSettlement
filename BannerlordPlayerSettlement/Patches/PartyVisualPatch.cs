
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Saves;
using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using Helpers;

using SandBox.View.Map;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using SandBox.ViewModelCollection;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View;

namespace BannerlordPlayerSettlement.Patches
{

    [HarmonyPatch(typeof(SettlementVisual))]
    public static class PartyVisualPatch
    {
        static MethodInfo SetStrategicEntity = AccessTools.Property(typeof(SettlementVisual), "StrategicEntity").SetMethod;
        static MethodInfo SetTownPhysicalEntities = AccessTools.Property(typeof(SettlementVisual), "TownPhysicalEntities").SetMethod;
        static MethodInfo SetCircleLocalFrame = AccessTools.Property(typeof(SettlementVisual), "CircleLocalFrame").SetMethod;

        static MethodInfo GetMapScene = AccessTools.Property(typeof(SettlementVisual), "MapScene").GetMethod;

        static MethodInfo PopulateSiegeEngineFrameListsFromChildren = AccessTools.Method(typeof(SettlementVisual), "PopulateSiegeEngineFrameListsFromChildren");
        static MethodInfo UpdateDefenderSiegeEntitiesCache = AccessTools.Method(typeof(SettlementVisual), "UpdateDefenderSiegeEntitiesCache");
        //static MethodInfo InitializePartyCollider = AccessTools.Method(typeof(MobilePartyVisual), "InitializePartyCollider");

        static FastInvokeHandler AddNewPartyVisualForPartyInvoker = MethodInvoker.GetHandler(AccessTools.Method(typeof(SettlementVisualManager), nameof(AddNewPartyVisualForParty)));

        public static void AddNewPartyVisualForParty(this SettlementVisualManager partyVisualManager, PartyBase partyBase)
        {
            AddNewPartyVisualForPartyInvoker(partyVisualManager, partyBase);
        }

        public static Scene MapScene(this SettlementVisual __instance)
        {
            return (Scene) GetMapScene.Invoke(__instance, null);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(OnMapHoverSiegeEngine))]
        public static bool OnMapHoverSiegeEngine(ref SettlementVisual __instance, ref GameEntity[] ____attackerBatteringRamSpawnEntities, ref GameEntity[] ____defenderBreachableWallEntitiesCacheForCurrentLevel, ref GameEntity[] ____defenderRangedEngineSpawnEntitiesCacheForCurrentLevel, ref GameEntity[] ____attackerRangedEngineSpawnEntities, ref GameEntity[] ____attackerSiegeTowerSpawnEntities, ref MatrixFrame ____hoveredSiegeEntityFrame, MatrixFrame engineFrame)
        {
            try
            {
                bool isPlayerSettlement = (__instance.MapEntity != null && __instance.MapEntity.Settlement.IsPlayerBuilt());
                bool playerSiegeEvent = (PlayerSiege.PlayerSiegeEvent != null && (PlayerSiege.PlayerSiegeEvent.BesiegedSettlement.IsPlayerBuilt() || PlayerSiege.PlayerSiegeEvent.BesiegedSettlement.IsOverwritten(out OverwriteSettlementItem overwriteSettlementItem)));
                if (!playerSiegeEvent && !isPlayerSettlement)
                {
                    return true;
                }


                if (PlayerSiege.PlayerSiegeEvent == null)
                {
                    return true;
                }
                try
                {
                    for (int i = 0; i < (int) ____attackerBatteringRamSpawnEntities.Length; i++)
                    {
                        MatrixFrame globalFrame = ____attackerBatteringRamSpawnEntities[i].GetGlobalFrame();
                        if (globalFrame.NearlyEquals(engineFrame, 1E-05f))
                        {
                            if (____hoveredSiegeEntityFrame != globalFrame)
                            {
                                SiegeEvent.SiegeEngineConstructionProgress deployedMeleeSiegeEngines = PlayerSiege.PlayerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker).SiegeEngines.DeployedMeleeSiegeEngines[i];
                                InformationManager.ShowTooltip(typeof(List<TooltipProperty>), new Object[] { SandBoxUIHelper.GetSiegeEngineInProgressTooltip(deployedMeleeSiegeEngines) });
                            }
                            return false;
                        }
                    }
                    for (int j = 0; j < (int) ____attackerSiegeTowerSpawnEntities.Length; j++)
                    {
                        MatrixFrame matrixFrame = ____attackerSiegeTowerSpawnEntities[j].GetGlobalFrame();
                        if (matrixFrame.NearlyEquals(engineFrame, 1E-05f))
                        {
                            if (____hoveredSiegeEntityFrame != matrixFrame)
                            {
                                SiegeEvent.SiegeEngineConstructionProgress siegeEngineConstructionProgress = PlayerSiege.PlayerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker).SiegeEngines.DeployedMeleeSiegeEngines[(int) ____attackerBatteringRamSpawnEntities.Length + j];
                                InformationManager.ShowTooltip(typeof(List<TooltipProperty>), new Object[] { SandBoxUIHelper.GetSiegeEngineInProgressTooltip(siegeEngineConstructionProgress) });
                            }
                            return false;
                        }
                    }
                    for (int k = 0; k < (int) ____attackerRangedEngineSpawnEntities.Length; k++)
                    {
                        MatrixFrame globalFrame1 = ____attackerRangedEngineSpawnEntities[k].GetGlobalFrame();
                        if (globalFrame1.NearlyEquals(engineFrame, 1E-05f))
                        {
                            if (____hoveredSiegeEntityFrame != globalFrame1)
                            {
                                SiegeEvent.SiegeEngineConstructionProgress deployedRangedSiegeEngines = PlayerSiege.PlayerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker).SiegeEngines.DeployedRangedSiegeEngines[k];
                                InformationManager.ShowTooltip(typeof(List<TooltipProperty>), new Object[] { SandBoxUIHelper.GetSiegeEngineInProgressTooltip(deployedRangedSiegeEngines) });
                            }
                            return false;
                        }
                    }
                    for (int l = 0; l < (int) ____defenderRangedEngineSpawnEntitiesCacheForCurrentLevel.Length; l++)
                    {
                        MatrixFrame matrixFrame1 = ____defenderRangedEngineSpawnEntitiesCacheForCurrentLevel[l].GetGlobalFrame();
                        if (matrixFrame1.NearlyEquals(engineFrame, 1E-05f))
                        {
                            if (____hoveredSiegeEntityFrame != matrixFrame1)
                            {
                                SiegeEvent.SiegeEngineConstructionProgress deployedRangedSiegeEngines1 = PlayerSiege.PlayerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Defender).SiegeEngines.DeployedRangedSiegeEngines[l];
                                InformationManager.ShowTooltip(typeof(List<TooltipProperty>), new Object[] { SandBoxUIHelper.GetSiegeEngineInProgressTooltip(deployedRangedSiegeEngines1) });
                            }
                            return false;
                        }
                    }
                    for (int m = 0; m < (int) ____defenderBreachableWallEntitiesCacheForCurrentLevel.Length; m++)
                    {
                        MatrixFrame globalFrame2 = ____defenderBreachableWallEntitiesCacheForCurrentLevel[m].GetGlobalFrame();
                        if (globalFrame2.NearlyEquals(engineFrame, 1E-05f))
                        {
                            if (____hoveredSiegeEntityFrame != globalFrame2 && (__instance as MapEntityVisual<PartyBase>).MapEntity.IsSettlement)
                            {
                                InformationManager.ShowTooltip(typeof(List<TooltipProperty>), new Object[] { SandBoxUIHelper.GetWallSectionTooltip((__instance as MapEntityVisual<PartyBase>).MapEntity.Settlement, m) });
                            }
                            return false;
                        }
                    }
                    ____hoveredSiegeEntityFrame = MatrixFrame.Identity;
                }
                catch (Exception e)
                {
                    // Silent fail here, do not fall back to default
                    LogManager.Log.Info(e.ToString());
                }

                return false;
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }

            return true;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(OnMapHoverSiegeEngine))]
        public static Exception? FixOnMapHoverSiegeEngine(Exception? __exception, ref SettlementVisual __instance)
        {
            if (__exception != null)
            {
                var e = __exception;
                LogManager.Log.NotifyBad(e);
            }
            return null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(OnStartup))]
        public static bool OnStartup(ref SettlementVisual __instance, ref Dictionary<int, List<GameEntity>> ____gateBannerEntitiesWithLevels)
        {
            try
            {
                OverwriteSettlementItem? overwriteItem = null;
                bool isPlayerSettlement = (__instance.MapEntity != null && __instance.MapEntity.Settlement.IsPlayerBuilt());
                bool isOverwrite = (__instance.MapEntity != null && __instance.MapEntity.Settlement.IsOverwritten(out overwriteItem));
                if (!isPlayerSettlement && !isOverwrite)
                {
                    return true;
                }
                List<MatrixFrame> matricesFrame;
                List<MatrixFrame> matricesFrame1;
                bool flag = false;

                if (!isOverwrite)
                {
                    SetStrategicEntity.Invoke(__instance, new object[] { __instance.MapScene().GetCampaignEntityWithName(__instance.MapEntity.Id) });
                }
                if (__instance.StrategicEntity == null)
                {
                    IMapScene mapSceneWrapper = Campaign.Current.MapSceneWrapper;
                    string stringId = (__instance as MapEntityVisual<PartyBase>).MapEntity.Settlement.StringId;
                    CampaignVec2 position = (__instance as MapEntityVisual<PartyBase>).MapEntity.Settlement.Position;
                    mapSceneWrapper.AddNewEntityToMapScene(stringId, in position);
                    SetStrategicEntity.Invoke(__instance, new object[] { __instance.MapScene().GetCampaignEntityWithName((__instance as MapEntityVisual<PartyBase>).MapEntity.Id) });
                }

                if (__instance.StrategicEntity != null && overwriteItem == null)
                {
                    var playerSettlementItem = PlayerSettlementInfo.Instance?.FindSettlement(__instance.MapEntity.Settlement);
                    if (playerSettlementItem?.RotationMat3 != null)
                    {
                        var frame = __instance.StrategicEntity.GetFrame();
                        frame.rotation = playerSettlementItem.RotationMat3;
                        __instance.StrategicEntity.SetFrame(ref frame);
                    }
                    if (playerSettlementItem?.DeepEdits != null)
                    {
                        var settlementVisualEntity = __instance.StrategicEntity;
                        List<GameEntity> settlementVisualEntityChildren = new();
                        settlementVisualEntity.GetChildrenRecursive(ref settlementVisualEntityChildren);

                        foreach (var dte in playerSettlementItem.DeepEdits)
                        {
                            var entity = dte.Index < 0 ? settlementVisualEntity : settlementVisualEntityChildren[dte.Index];
                            var local = entity!.GetFrame();
                            local.rotation = dte?.Transform?.RotationScale != null ? dte.Transform.RotationScale : local.rotation;
                            if (dte!.Index >= 0)
                            {
                                local.origin = dte?.Transform?.Position != null ? dte.Transform.Position : local.origin;
                            }
                            else
                            {
                                local.origin = dte?.Transform?.Offsets != null ? local.origin + dte.Transform.Offsets : local.origin;
                            }

                            entity.SetFrame(ref local);
                        }

                        try
                        {
                            // After updating all edits, remove the ones marked as deleted (in reverse to avoid child deletes interfering)
                            foreach (var dte in playerSettlementItem.DeepEdits.AsEnumerable().Reverse().Where(d => d.IsDeleted && d.Index >= 0))
                            {
                                if (dte.Index < 0)
                                {
                                    continue;
                                }
                                var entity = settlementVisualEntityChildren[dte.Index];

                                // Delete submodel that has been marked as deleted
                                entity.ClearEntity();
                            }
                        }
                        catch (Exception e)
                        {
                            LogManager.EventTracer.Trace(new List<string> { e.Message, e.StackTrace });
                        }
                    }
                }
                if (__instance.StrategicEntity != null && overwriteItem != null)
                {
                    if (overwriteItem?.RotationMat3 != null)
                    {
                        var frame = __instance.StrategicEntity.GetFrame();
                        frame.rotation = overwriteItem.RotationMat3;
                        __instance.StrategicEntity.SetFrame(ref frame);
                    }
                    if (overwriteItem?.DeepEdits != null)
                    {
                        var settlementVisualEntity = __instance.StrategicEntity;
                        List<GameEntity> settlementVisualEntityChildren = new();
                        settlementVisualEntity.GetChildrenRecursive(ref settlementVisualEntityChildren);

                        foreach (var dte in overwriteItem.DeepEdits)
                        {
                            var entity = dte.Index < 0 ? settlementVisualEntity : settlementVisualEntityChildren[dte.Index];
                            var local = entity!.GetFrame();
                            local.rotation = dte?.Transform?.RotationScale != null ? dte.Transform.RotationScale : local.rotation;
                            if (dte!.Index >= 0)
                            {
                                local.origin = dte?.Transform?.Position != null ? dte.Transform.Position : local.origin;
                            }
                            else
                            {
                                local.origin = dte?.Transform?.Offsets != null ? local.origin + dte.Transform.Offsets : local.origin;
                            }

                            entity.SetFrame(ref local);
                        }

                        try
                        {
                            // After updating all edits, remove the ones marked as deleted (in reverse to avoid child deletes interfering)
                            foreach (var dte in overwriteItem.DeepEdits.AsEnumerable().Reverse().Where(d => d.IsDeleted && d.Index >= 0))
                            {
                                if (dte.Index < 0)
                                {
                                    continue;
                                }
                                var entity = settlementVisualEntityChildren[dte.Index];

                                // Delete submodel that has been marked as deleted
                                entity.ClearEntity();
                            }
                        }
                        catch (Exception e)
                        {
                            LogManager.EventTracer.Trace(new List<string> { e.Message, e.StackTrace });
                        }
                    }
                }

                bool flag1 = false;
                if ((__instance as MapEntityVisual<PartyBase>).MapEntity.Settlement.IsFortification)
                {
                    List<GameEntity> gameEntities = new List<GameEntity>();
                    __instance.StrategicEntity.GetChildrenRecursive(ref gameEntities);



                    PopulateSiegeEngineFrameListsFromChildren.Invoke(__instance, new object[] { gameEntities });
                    UpdateDefenderSiegeEntitiesCache.Invoke(__instance, null);
                    SetTownPhysicalEntities.Invoke(__instance, new object[] { gameEntities.FindAll((GameEntity x) => x.HasTag("bo_town")) });
                    List<GameEntity> gameEntities1 = new List<GameEntity>();
                    Dictionary<int, List<GameEntity>> nums = new Dictionary<int, List<GameEntity>>()
                {
                    { 1, new List<GameEntity>() },
                    { 2, new List<GameEntity>() },
                    { 3, new List<GameEntity>() }
                };
                    foreach (GameEntity gameEntity in gameEntities)
                    {
                        if (gameEntity.HasTag("main_map_city_gate"))
                        {
                            MatrixFrame globalFrame = gameEntity.GetGlobalFrame();
                            NavigationHelper.IsPositionValidForNavigationType(new CampaignVec2(globalFrame.origin.AsVec2, true), MobileParty.NavigationType.Default);
                            flag1 = true;
                            gameEntities1.Add(gameEntity);
                        }
                        if (gameEntity.HasTag("map_settlement_circle"))
                        {
                            SetCircleLocalFrame.Invoke(__instance, new object[] { gameEntity.GetGlobalFrame() });
                            flag = true;
                            gameEntity.SetVisibilityExcludeParents(false);
                            gameEntities1.Add(gameEntity);
                        }
                        if (!gameEntity.HasTag("map_banner_placeholder"))
                        {
                            continue;
                        }
                        int upgradeLevelOfEntity = gameEntity.Parent.GetUpgradeLevelOfEntity();
                        if (upgradeLevelOfEntity != 0)
                        {
                            nums[upgradeLevelOfEntity].Add(gameEntity);
                        }
                        else
                        {
                            nums[1].Add(gameEntity);
                            nums[2].Add(gameEntity);
                            nums[3].Add(gameEntity);
                        }
                        gameEntities1.Add(gameEntity);
                    }
                    ____gateBannerEntitiesWithLevels = nums;
                    if ((__instance as MapEntityVisual<PartyBase>).MapEntity.Settlement.IsFortification)
                    {
                        Campaign.Current.MapSceneWrapper.GetSiegeCampFrames((__instance as MapEntityVisual<PartyBase>).MapEntity.Settlement, out matricesFrame, out matricesFrame1);
                        (__instance as MapEntityVisual<PartyBase>).MapEntity.Settlement.Town.BesiegerCampPositions1 = matricesFrame.ToArray();
                        (__instance as MapEntityVisual<PartyBase>).MapEntity.Settlement.Town.BesiegerCampPositions2 = matricesFrame1.ToArray();
                    }
                    foreach (GameEntity gameEntity1 in gameEntities1)
                    {
                        gameEntity1.Remove(112);
                    }
                    if (!flag1 && !(__instance as MapEntityVisual<PartyBase>).MapEntity.Settlement.IsTown)
                    {
                        bool isCastle = (__instance as MapEntityVisual<PartyBase>).MapEntity.Settlement.IsCastle;
                    }
                    bool flag2 = false;
                    if ((__instance as MapEntityVisual<PartyBase>).MapEntity.IsSettlement)
                    {
                        foreach (GameEntity child in __instance.StrategicEntity.GetChildren())
                        {
                            if (!child.HasTag("main_map_city_port"))
                            {
                                continue;
                            }
                            MatrixFrame matrixFrame = child.GetGlobalFrame();
                            NavigationHelper.IsPositionValidForNavigationType(new CampaignVec2(matrixFrame.origin.AsVec2, false), MobileParty.NavigationType.Naval);
                            flag2 = true;
                        }
                        if ((flag2 || !(__instance as MapEntityVisual<PartyBase>).MapEntity.Settlement.HasPort) && flag2)
                        {
                            bool hasPort = (__instance as MapEntityVisual<PartyBase>).MapEntity.Settlement.HasPort;
                        }
                    }
                }
                if (!flag)
                {
                    SetCircleLocalFrame.Invoke(__instance, new object[] { MatrixFrame.Identity });
                    MatrixFrame circleLocalFrame = __instance.CircleLocalFrame;
                    Mat3 mat3 = circleLocalFrame.rotation;
                    if ((__instance as MapEntityVisual<PartyBase>).MapEntity.Settlement.IsVillage)
                    {
                        mat3.ApplyScaleLocal(1.75f);
                    }
                    else if ((__instance as MapEntityVisual<PartyBase>).MapEntity.Settlement.IsTown)
                    {
                        mat3.ApplyScaleLocal(5.75f);
                    }
                    else if (!(__instance as MapEntityVisual<PartyBase>).MapEntity.Settlement.IsCastle)
                    {
                        mat3.ApplyScaleLocal(1.75f);
                    }
                    else
                    {
                        mat3.ApplyScaleLocal(2.75f);
                    }
                    circleLocalFrame.rotation = mat3;
                    SetCircleLocalFrame.Invoke(__instance, new object[] { circleLocalFrame });
                }
                __instance.StrategicEntity.SetVisibilityExcludeParents((__instance as MapEntityVisual<PartyBase>).MapEntity.IsVisible);
                __instance.StrategicEntity.SetReadyToRender(true);
                __instance.StrategicEntity.SetEntityEnvMapVisibility(false);
                List<GameEntity> gameEntities2 = new List<GameEntity>();
                __instance.StrategicEntity.GetChildrenRecursive(ref gameEntities2);
                if (!MapScreen.VisualsOfEntities.ContainsKey(__instance.StrategicEntity.Pointer))
                {
                    MapScreen.VisualsOfEntities.Add(__instance.StrategicEntity.Pointer, __instance);
                }
                foreach (GameEntity gameEntity2 in gameEntities2)
                {
                    if (MapScreen.VisualsOfEntities.ContainsKey(gameEntity2.Pointer) || MapScreenPatch.FrameAndVisualOfEngines().ContainsKey(gameEntity2.Pointer))
                    {
                        continue;
                    }
                    MapScreen.VisualsOfEntities.Add(gameEntity2.Pointer, __instance);
                }
                __instance.StrategicEntity.SetAsPredisplayEntity();

                return false;
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }

            return true;
        }


        [HarmonyFinalizer]
        //[HarmonyPatch(nameof(SettlementVisual.OnStartup))]
        [HarmonyPatch(nameof(OnStartup))]
        public static Exception FixOnStartup(object __exception, ref SettlementVisual __instance)
        {
            if (__exception != null)
            {
                var e = __exception;
                if (e != null)
                {
                    if (e is Exception ex)
                    {

                        LogManager.Log.NotifyBad(ex);
                    }
                    else
                    {

                        LogManager.Log.NotifyBad(e.ToString());
                    }
                }
            }
            return null;
        }
    }
}
