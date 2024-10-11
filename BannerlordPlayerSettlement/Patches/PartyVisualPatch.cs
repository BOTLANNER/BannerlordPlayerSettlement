
using System;
using System.Collections.Generic;
using System.Reflection;

using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Saves;

using HarmonyLib;

using Helpers;

using SandBox.View.Map;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View;

namespace BannerlordPlayerSettlement.Patches
{

    [HarmonyPatch(typeof(PartyVisual))]
    public static class PartyVisualPatch
    {
        static MethodInfo SetStrategicEntity = AccessTools.Property(typeof(PartyVisual), "StrategicEntity").SetMethod;
        static MethodInfo SetTownPhysicalEntities = AccessTools.Property(typeof(PartyVisual), "TownPhysicalEntities").SetMethod;
        static MethodInfo SetCircleLocalFrame = AccessTools.Property(typeof(PartyVisual), "CircleLocalFrame").SetMethod;

        static MethodInfo GetMapScene = AccessTools.Property(typeof(PartyVisual), "MapScene").GetMethod;

        static MethodInfo PopulateSiegeEngineFrameListsFromChildren = AccessTools.Method(typeof(PartyVisual), "PopulateSiegeEngineFrameListsFromChildren");
        static MethodInfo UpdateDefenderSiegeEntitiesCache = AccessTools.Method(typeof(PartyVisual), "UpdateDefenderSiegeEntitiesCache");
        static MethodInfo InitializePartyCollider = AccessTools.Method(typeof(PartyVisual), "InitializePartyCollider");

        static FastInvokeHandler AddNewPartyVisualForPartyInvoker = MethodInvoker.GetHandler(AccessTools.Method(typeof(PartyVisualManager), nameof(AddNewPartyVisualForParty)));

        public static void AddNewPartyVisualForParty(this PartyVisualManager partyVisualManager, PartyBase partyBase)
        {
            AddNewPartyVisualForPartyInvoker(partyVisualManager, partyBase );
        }

        public static Scene MapScene(this PartyVisual __instance)
        {
            return (Scene) GetMapScene.Invoke(__instance, null);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(OnMapHoverSiegeEngine))]
        public static bool OnMapHoverSiegeEngine(ref PartyVisual __instance, MatrixFrame engineFrame)
        {
            try
            {
                bool isPlayerSettlement = (__instance.PartyBase != null && __instance.PartyBase.Settlement.IsPlayerBuilt());
                bool playerSiegeEvent = (PlayerSiege.PlayerSiegeEvent != null && PlayerSiege.PlayerSiegeEvent.BesiegedSettlement.IsPlayerBuilt());
                if (!playerSiegeEvent && !isPlayerSettlement)
                {
                    return true;
                }

                return false;
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }

            return true;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(OnMapHoverSiegeEngine))]
        public static Exception? FixOnMapHoverSiegeEngine(ref Exception __exception, ref PartyVisual __instance)
        {
            if (__exception != null)
            {
                var e = __exception;
                TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace);
                Debug.WriteDebugLineOnScreen(e.ToString());
                Debug.SetCrashReportCustomString(e.Message);
                Debug.SetCrashReportCustomStack(e.StackTrace);
            }
            return null;
        }
        [HarmonyPrefix]
        [HarmonyPatch(nameof(OnStartup))]
        public static bool OnStartup(ref PartyVisual __instance, ref Dictionary<int, List<GameEntity>> ____gateBannerEntitiesWithLevels, ref float ____entityAlpha)
        {
            try
            {
                bool isPlayerSettlement = (__instance.PartyBase != null && __instance.PartyBase.Settlement.IsPlayerBuilt());
                if (!isPlayerSettlement)
                {
                    return true;
                }
                bool flag = false;
                if (__instance.PartyBase.IsMobile)
                {
                    SetStrategicEntity.Invoke(__instance, new object[] { GameEntity.CreateEmpty(__instance.MapScene(), true) });
                    if (!__instance.PartyBase.IsVisible)
                    {
                        GameEntity strategicEntity = __instance.StrategicEntity;
                        strategicEntity.EntityFlags = strategicEntity.EntityFlags | EntityFlags.DoNotTick;
                    }
                }
                else if (__instance.PartyBase.IsSettlement)
                {
                    SetStrategicEntity.Invoke(__instance, new object[] { __instance.MapScene().GetCampaignEntityWithName(__instance.PartyBase.Id) });
                    if (__instance.StrategicEntity == null)
                    {
                        Campaign.Current.MapSceneWrapper.AddNewEntityToMapScene(__instance.PartyBase.Settlement.StringId, __instance.PartyBase.Settlement.Position2D);
                        SetStrategicEntity.Invoke(__instance, new object[] { __instance.MapScene().GetCampaignEntityWithName(__instance.PartyBase.Id) });
                    }

                    if (__instance.StrategicEntity != null)
                    {
                        var playerSettlementItem = PlayerSettlementInfo.Instance?.FindSettlement(__instance.PartyBase.Settlement);
                        if (playerSettlementItem?.RotationMat3 != null)
                        {
                            var frame = __instance.StrategicEntity.GetFrame();
                            frame.rotation = playerSettlementItem.RotationMat3;
                            __instance.StrategicEntity.SetFrame(ref frame);
                        }
                    }
                    bool flag1 = false;
                    if (__instance.PartyBase.Settlement.IsFortification)
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
                                MatrixFrame globalFrame = gameEntity.GetGlobalFrame();
                                PartyBase.IsPositionOkForTraveling(globalFrame.origin.AsVec2);
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
                        if (__instance.PartyBase.Settlement.IsFortification)
                        {
                            __instance.PartyBase.Settlement.Town.BesiegerCampPositions1 = matricesFrame.ToArray();
                            __instance.PartyBase.Settlement.Town.BesiegerCampPositions2 = matricesFrame1.ToArray();
                        }
                        foreach (GameEntity gameEntity1 in gameEntities1)
                        {
                            gameEntity1.Remove(112);
                        }
                    }
                    if (!flag1)
                    {
                        if (!__instance.PartyBase.Settlement.IsTown)
                        {
                            bool isCastle = __instance.PartyBase.Settlement.IsCastle;
                        }
                        if (!PartyBase.IsPositionOkForTraveling(__instance.PartyBase.Settlement.GatePosition))
                        {
                            Vec2 gatePosition = __instance.PartyBase.Settlement.GatePosition;
                        }
                    }
                }
                CharacterObject visualPartyLeader = PartyBaseHelper.GetVisualPartyLeader(__instance.PartyBase);
                if (!flag)
                {
                    SetCircleLocalFrame.Invoke(__instance, new object[] { MatrixFrame.Identity });
                    if (__instance.PartyBase.IsSettlement)
                    {
                        MatrixFrame circleLocalFrame = __instance.CircleLocalFrame;
                        Mat3 mat3 = circleLocalFrame.rotation;
                        if (__instance.PartyBase.Settlement.IsVillage)
                        {
                            mat3.ApplyScaleLocal(1.75f);
                        }
                        else if (__instance.PartyBase.Settlement.IsTown)
                        {
                            mat3.ApplyScaleLocal(5.75f);
                        }
                        else if (!__instance.PartyBase.Settlement.IsCastle)
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
                    else if ((visualPartyLeader == null || !visualPartyLeader.HasMount()) && !__instance.PartyBase.MobileParty.IsCaravan)
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
                __instance.StrategicEntity.SetVisibilityExcludeParents(__instance.PartyBase.IsVisible);
                AgentVisuals humanAgentVisuals = __instance.HumanAgentVisuals;
                if (humanAgentVisuals != null)
                {
                    GameEntity entity = humanAgentVisuals.GetEntity();
                    if (entity != null)
                    {
                        entity.SetVisibilityExcludeParents(__instance.PartyBase.IsVisible);
                    }
                    else
                    {
                    }
                }
                else
                {
                }
                AgentVisuals mountAgentVisuals = __instance.MountAgentVisuals;
                if (mountAgentVisuals != null)
                {
                    GameEntity entity1 = mountAgentVisuals.GetEntity();
                    if (entity1 != null)
                    {
                        entity1.SetVisibilityExcludeParents(__instance.PartyBase.IsVisible);
                    }
                    else
                    {
                    }
                }
                else
                {
                }
                AgentVisuals caravanMountAgentVisuals = __instance.CaravanMountAgentVisuals;
                if (caravanMountAgentVisuals != null)
                {
                    GameEntity entity2 = caravanMountAgentVisuals.GetEntity();
                    if (entity2 != null)
                    {
                        entity2.SetVisibilityExcludeParents(__instance.PartyBase.IsVisible);
                    }
                    else
                    {
                    }
                }
                else
                {
                }
                __instance.StrategicEntity.SetReadyToRender(true);
                __instance.StrategicEntity.SetEntityEnvMapVisibility(false);
                ____entityAlpha = (__instance.PartyBase.IsVisible ? 1f : 0f);
                InitializePartyCollider.Invoke(__instance, new object[] { __instance.PartyBase });
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
                if (__instance.PartyBase.IsSettlement)
                {
                    __instance.StrategicEntity.SetAsPredisplayEntity();
                }

                return false;
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }

            return true;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(PartyVisual.OnStartup))]
        public static Exception? FixOnStartup(ref Exception __exception, ref PartyVisual __instance)
        {
            if (__exception != null)
            {
                var e = __exception;
                TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace);
                Debug.WriteDebugLineOnScreen(e.ToString());
                Debug.SetCrashReportCustomString(e.Message);
                Debug.SetCrashReportCustomStack(e.StackTrace);
            }
            return null;
        }
    }
}
