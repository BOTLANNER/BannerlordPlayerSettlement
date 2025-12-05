
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.UI.Viewmodels;

using HarmonyLib;

using SandBox.View.Map;
using SandBox.View.Map.Visuals;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Patches
{

    [HarmonyPatch(typeof(MapScreen))]
    public static class MapScreenPatch
    {

        static MethodInfo GetFrameAndVisualOfEngines = AccessTools.Property(typeof(MapScreen), "FrameAndVisualOfEngines").GetMethod;
        public static Dictionary<UIntPtr, Tuple<MatrixFrame, SettlementVisual>> FrameAndVisualOfEngines()
        {
            return (Dictionary<UIntPtr, Tuple<MatrixFrame, SettlementVisual>>) GetFrameAndVisualOfEngines.Invoke(null, null);
        }


        static MethodInfo GetDesiredDecalColorMethod = AccessTools.Method(typeof(MapScreen), "GetDesiredDecalColor");

        public static uint GetDesiredDecalColor(this MapScreen mapScreen, bool isPrepOver, bool isHovered, bool isEnemy, bool isEmpty, bool isPlayerLeader)
        {
            return (uint) GetDesiredDecalColorMethod.Invoke(mapScreen, new object[] { isPrepOver, isHovered, isEnemy, isEmpty, isPlayerLeader });
        }


        static MethodInfo GetDesiredMaterialNameMethod = AccessTools.Method(typeof(MapScreen), "GetDesiredMaterialName");

        public static string GetDesiredMaterialName(this MapScreen mapScreen, bool isRanged, bool isAttacker, bool isEmpty, bool isTower)
        {
            return (string) GetDesiredMaterialNameMethod.Invoke(mapScreen, new object[] { isRanged, isAttacker, isEmpty, isTower });
        }

        static MethodInfo IsEscapedMethod = AccessTools.Method(typeof(MapView), "IsEscaped") ?? AccessTools.DeclaredMethod(typeof(MapView), "IsEscaped");

        static bool CheckIsEscaped(this MapView mapView)
        {
            try
            {
                return (bool) IsEscapedMethod.Invoke(mapView, new object[] { });
            }
            catch (Exception e)
            {
                // No logging, this is default behaviour
                return false;
            }
        }

        static MapScreenPatch()
        {
            Main.Harmony?.Patch(AccessTools.DeclaredMethod(typeof(MapScreen), "TaleWorlds.CampaignSystem.GameState.IMapStateHandler.AfterWaitTick"), prefix: new HarmonyMethod(typeof(MapScreenPatch), nameof(AfterWaitTick)));
        }

        public static bool AfterWaitTick(ref MapScreen __instance, ref MapViewsContainer ____mapViewsContainer, float dt)
        {
            if (__instance.SceneLayer.Input.IsHotKeyReleased("ToggleEscapeMenu") &&
                    PlayerSettlementBehaviour.Instance != null &&
                    (PlayerSettlementBehaviour.Instance.IsPlacingSettlement ||
                        PlayerSettlementBehaviour.Instance.IsPlacingGate))
            {
                if (!____mapViewsContainer.IsThereAnyViewIsEscaped())
                {
                    if (PlayerSettlementBehaviour.Instance.IsPlacingGate)
                    {
                        PlayerSettlementBehaviour.Instance.RefreshVisualSelection();
                        return false;
                    }
                    PlayerSettlementBehaviour.Instance.Reset();
                    MapBarExtensionVM.Current?.OnRefresh();
                    return false;
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(HandleLeftMouseButtonClick))]
        public static bool HandleLeftMouseButtonClick(ref MapScreen __instance, MapEntityVisual visualOfSelectedEntity, CampaignVec2 intersectionPoint, PathFaceRecord mouseOverFaceIndex, bool isDoubleClick)
        {

            if (__instance.SceneLayer.Input.GetIsMouseActive() && PlayerSettlementBehaviour.Instance != null && PlayerSettlementBehaviour.Instance.IsPlacingGate && __instance.SceneLayer.ActiveCursor == TaleWorlds.ScreenSystem.CursorType.Default)
            {
                PlayerSettlementBehaviour.Instance.ApplyNow();
                return false;
            }
            else if (__instance.SceneLayer.Input.GetIsMouseActive() && PlayerSettlementBehaviour.Instance != null && PlayerSettlementBehaviour.Instance.IsPlacingSettlement && __instance.SceneLayer.ActiveCursor == TaleWorlds.ScreenSystem.CursorType.Default)
            {
                if (PlayerSettlementBehaviour.Instance.IsDeepEdit)
                {
                    // TODO: Determine if raycast could select a part?
                    return false;
                }
                PlayerSettlementBehaviour.Instance.StartGatePlacement();
                return false;
            }
            return true;
        } }
}