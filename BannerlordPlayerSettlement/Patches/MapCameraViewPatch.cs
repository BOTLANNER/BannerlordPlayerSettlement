
using BannerlordPlayerSettlement.Behaviours;

using HarmonyLib;

using SandBox.View.Map;

using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;

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
            if (PlayerSettlementBehaviour.Instance != null && 
                PlayerSettlementBehaviour.Instance.IsPlacingSettlement && 
                Game.Current.GameStateManager.ActiveState is MapState mapState && 
                mapState.Handler is MapScreen mapScreen &&
                (mapScreen.SceneLayer.Input.IsAltDown() ||
                        mapScreen.SceneLayer.Input.IsShiftDown())
                    //(inputInformation.RightMouseButtonDown ||
                    //    inputInformation.RotateLeftKeyDown ||
                    //    inputInformation.RotateRightKeyDown)
                    )
            {
                //if (
                //    (mapScreen.SceneLayer.Input.IsAltDown() ||
                //        mapScreen.SceneLayer.Input.IsShiftDown()))
                {
                    __result = true;
                    return false;
                }
            }
            return true;
        }

    }
}