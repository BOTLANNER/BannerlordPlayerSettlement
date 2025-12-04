
using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Saves;
using BannerlordPlayerSettlement.Utils;
using HarmonyLib;
using Helpers;
using SandBox.View.Map;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View;

namespace BannerlordPlayerSettlement.Patches
{

    [HarmonyPatch(typeof(SettlementVisual))]
    public static class SettlementVisualPatch
    {
        static MethodInfo SetStrategicEntity = AccessTools.Property(typeof(SettlementVisual), "StrategicEntity").SetMethod;
        static MethodInfo SetTownPhysicalEntities = AccessTools.Property(typeof(SettlementVisual), "TownPhysicalEntities").SetMethod;
        static MethodInfo SetCircleLocalFrame = AccessTools.Property(typeof(SettlementVisual), "CircleLocalFrame").SetMethod;

        static MethodInfo GetMapScene = AccessTools.Property(typeof(SettlementVisual), "MapScene").GetMethod;

        static MethodInfo PopulateSiegeEngineFrameListsFromChildren = AccessTools.Method(typeof(SettlementVisual), "PopulateSiegeEngineFrameListsFromChildren");
        static MethodInfo UpdateDefenderSiegeEntitiesCache = AccessTools.Method(typeof(SettlementVisual), "UpdateDefenderSiegeEntitiesCache");
        static MethodInfo InitializePartyCollider = AccessTools.Method(typeof(SettlementVisual), "InitializePartyCollider");

        static FastInvokeHandler AddNewSettlementVisualForPartyInvoker = MethodInvoker.GetHandler(AccessTools.Method(typeof(SettlementVisualManager), nameof(AddNewSettlementVisualForParty)));

        public static void AddNewSettlementVisualForParty(this SettlementVisualManager SettlementVisualManager, PartyBase partyBase)
        {
            AddNewSettlementVisualForPartyInvoker(SettlementVisualManager, partyBase );
        }

        public static Scene MapScene(this SettlementVisual __instance)
        {
            return (Scene) GetMapScene.Invoke(__instance, null);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(OnMapHoverSiegeEngine))]
        public static bool OnMapHoverSiegeEngine(ref SettlementVisual __instance, MatrixFrame engineFrame)
        {
            try
            {
                bool isPlayerSettlement = (__instance.MapEntity != null && __instance.MapEntity.Settlement.IsPlayerBuilt());
                bool playerSiegeEvent = (PlayerSiege.PlayerSiegeEvent != null && PlayerSiege.PlayerSiegeEvent.BesiegedSettlement.IsPlayerBuilt());
                if (!playerSiegeEvent && !isPlayerSettlement)
                {
                    return true;
                }

                return false;
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }

            return true;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(OnMapHoverSiegeEngine))]
        public static Exception? FixOnMapHoverSiegeEngine(ref Exception __exception, ref SettlementVisual __instance)
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
        public static bool OnStartup(ref SettlementVisual __instance, ref Dictionary<int, List<GameEntity>> ____gateBannerEntitiesWithLevels, ref float ____entityAlpha)
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
                bool flag = false;
                if (__instance.MapEntity.IsMobile)
                {
                    SetStrategicEntity.Invoke(__instance, new object[] { GameEntity.CreateEmpty(__instance.MapScene(), true) });
                    if (!__instance.MapEntity.IsVisible)
                    {
                        GameEntity strategicEntity = __instance.StrategicEntity;
                        strategicEntity.EntityFlags = strategicEntity.EntityFlags | EntityFlags.DoNotTick;
                    }
                }
                else if (__instance.MapEntity.IsSettlement)
                {
                    if (!isOverwrite)
                    {
                        SetStrategicEntity.Invoke(__instance, new object[] { __instance.MapScene().GetCampaignEntityWithName(__instance.MapEntity.Id) }); 
                    }
                    if (__instance.StrategicEntity == null)
                    {
                        Campaign.Current.MapSceneWrapper.AddNewEntityToMapScene(__instance.MapEntity.Settlement.StringId, __instance.MapEntity.Position);
                        SetStrategicEntity.Invoke(__instance, new object[] { __instance.MapScene().GetCampaignEntityWithName(__instance.MapEntity.Id) });
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
                    if (__instance.MapEntity.Settlement.IsFortification)
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
                        List<MatrixFrame> matricesFrame = new List<MatrixFrame>();
                        List<MatrixFrame> matricesFrame1 = new List<MatrixFrame>();
                        foreach (GameEntity gameEntity in gameEntities)
                        {
                            if (gameEntity.HasTag("main_map_city_gate"))
                            {
                                //MatrixFrame globalFrame = gameEntity.GetGlobalFrame();
                                //PartyBase.IsPositionOkForTraveling(globalFrame.origin.AsVec2);
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
                            if (gameEntity.HasTag("map_banner_placeholder"))
                            {
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
                            if (!gameEntity.HasTag("map_camp_area_1"))
                            {
                                if (!gameEntity.HasTag("map_camp_area_2"))
                                {
                                    continue;
                                }
                                matricesFrame1.Add(gameEntity.GetGlobalFrame());
                                gameEntities1.Add(gameEntity);
                            }
                            else
                            {
                                matricesFrame.Add(gameEntity.GetGlobalFrame());
                                gameEntities1.Add(gameEntity);
                            }
                        }
                        ____gateBannerEntitiesWithLevels = nums;
                        if (__instance.MapEntity.Settlement.IsFortification)
                        {
                            __instance.MapEntity.Settlement.Town.BesiegerCampPositions1 = matricesFrame.ToArray();
                            __instance.MapEntity.Settlement.Town.BesiegerCampPositions2 = matricesFrame1.ToArray();
                        }
                        foreach (GameEntity gameEntity1 in gameEntities1)
                        {
                            gameEntity1.Remove(112);
                        }
                    }
                    if (!flag1)
                    {
                        if (!__instance.MapEntity.Settlement.IsTown)
                        {
                            bool isCastle = __instance.MapEntity.Settlement.IsCastle;
                        }
                        //if (!PartyBase.IsPositionOkForTraveling(__instance.MapEntity.Settlement.GatePosition))
                        //{
                        //    Vec2 gatePosition = __instance.MapEntity.Settlement.GatePosition.ToVec2();
                        //}
                    }
                }
                CharacterObject visualPartyLeader = PartyBaseHelper.GetVisualPartyLeader(__instance.MapEntity);
                if (!flag)
                {
                    SetCircleLocalFrame.Invoke(__instance, new object[] { MatrixFrame.Identity });
                    if (__instance.MapEntity.IsSettlement)
                    {
                        MatrixFrame circleLocalFrame = __instance.CircleLocalFrame;
                        Mat3 mat3 = circleLocalFrame.rotation;
                        if (__instance.MapEntity.Settlement.IsVillage)
                        {
                            mat3.ApplyScaleLocal(1.75f);
                        }
                        else if (__instance.MapEntity.Settlement.IsTown)
                        {
                            mat3.ApplyScaleLocal(5.75f);
                        }
                        else if (!__instance.MapEntity.Settlement.IsCastle)
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
                    else if ((visualPartyLeader == null || !visualPartyLeader.HasMount()) && !__instance.MapEntity.MobileParty.IsCaravan)
                    {
                        MatrixFrame matrixFrame = __instance.CircleLocalFrame;
                        Mat3 mat31 = matrixFrame.rotation;
                        mat31.ApplyScaleLocal(0.3725f);
                        matrixFrame.rotation = mat31;
                        SetCircleLocalFrame.Invoke(__instance, new object[] { matrixFrame });
                    }
                    else
                    {
                        MatrixFrame circleLocalFrame1 = __instance.CircleLocalFrame;
                        Mat3 mat32 = circleLocalFrame1.rotation;
                        mat32.ApplyScaleLocal(0.4625f);
                        circleLocalFrame1.rotation = mat32;
                        SetCircleLocalFrame.Invoke(__instance, new object[] { circleLocalFrame1 });
                    }
                }
                __instance.StrategicEntity.SetVisibilityExcludeParents(__instance.MapEntity.IsVisible);
                //AgentVisuals humanAgentVisuals = __instance.HumanAgentVisuals;
                //if (humanAgentVisuals != null)
                //{
                //    GameEntity entity = humanAgentVisuals.GetEntity();
                //    if (entity != null)
                //    {
                //        entity.SetVisibilityExcludeParents(__instance.MapEntity.IsVisible);
                //    }
                //    else
                //    {
                //    }
                //}
                //else
                //{
                //}
                //AgentVisuals mountAgentVisuals = __instance.MountAgentVisuals;
                //if (mountAgentVisuals != null)
                //{
                //    GameEntity entity1 = mountAgentVisuals.GetEntity();
                //    if (entity1 != null)
                //    {
                //        entity1.SetVisibilityExcludeParents(__instance.MapEntity.IsVisible);
                //    }
                //    else
                //    {
                //    }
                //}
                //else
                //{
                //}
                //AgentVisuals caravanMountAgentVisuals = __instance.CaravanMountAgentVisuals;
                //if (caravanMountAgentVisuals != null)
                //{
                //    GameEntity entity2 = caravanMountAgentVisuals.GetEntity();
                //    if (entity2 != null)
                //    {
                //        entity2.SetVisibilityExcludeParents(__instance.MapEntity.IsVisible);
                //    }
                //    else
                //    {
                //    }
                //}
                //else
                //{
                //}
                __instance.StrategicEntity.SetReadyToRender(true);
                __instance.StrategicEntity.SetEntityEnvMapVisibility(false);
                ____entityAlpha = (__instance.MapEntity.IsVisible ? 1f : 0f);
                InitializePartyCollider.Invoke(__instance, new object[] { __instance.MapEntity });
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
                if (__instance.MapEntity.IsSettlement)
                {
                    __instance.StrategicEntity.SetAsPredisplayEntity();
                }

                return false;
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }

            return true;
        }

        //[HarmonyFinalizer]
        //[HarmonyPatch(nameof(SettlementVisual.OnStartup))]
        //public static Exception? FixOnStartup(ref Exception __exception, ref SettlementVisual __instance)
        //{
        //    if (__exception != null)
        //    {
        //        var e = __exception;
        //        LogManager.Log.NotifyBad(e);
        //    }
        //    return null;
        //}
    }
}
