
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Extensions;

using HarmonyLib;

using SandBox.View.Map;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(MapCameraView))]
    public static class MapCameraViewPatch
    {

        [HarmonyPrefix]
        [HarmonyPatch(nameof(OnBeforeTick))]
        public static bool OnBeforeTick(ref MapCameraView __instance, ref MapCameraView.InputInformation inputInformation)
        {
            PlayerSettlementBehaviour.Instance?.OnBeforeTick(ref inputInformation);
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(GetMapCameraInput))]
        public static bool GetMapCameraInput(ref bool __result, ref MapCameraView __instance, MapCameraView.InputInformation inputInformation)
        {
            if (PlayerSettlementBehaviour.Instance != null && PlayerSettlementBehaviour.Instance.IsPlacingSettlement &&
                (inputInformation.RightMouseButtonDown ||
                    inputInformation.RotateLeftKeyDown ||
                    inputInformation.RotateRightKeyDown))
            {
                if (Game.Current.GameStateManager.ActiveState is MapState mapState &&
                    mapState.Handler is MapScreen mapScreen &&
                    (Main.Settings?.RotationAltModifier ?? true ?
                        mapScreen.SceneLayer.Input.IsAltDown() :
                        mapScreen.SceneLayer.Input.IsShiftDown()))
                {
                    __result = true;
                    return false;
                }
            }
            return true;
        }

    }
}