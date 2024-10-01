
using BannerlordPlayerSettlement.Behaviours;

using HarmonyLib;

using SandBox;

using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(MapScene))]
    public static class MapScenePatch
    {

        [HarmonyPrefix]
        [HarmonyPatch(nameof(AddNewEntityToMapScene))]
        public static bool AddNewEntityToMapScene(ref MapScene __instance, ref Scene ____scene, ref string entityId,ref Vec2 position)
        {
            try
            {
                if (entityId == PlayerSettlementBehaviour.PlayerSettlementIdentifier)
                {
                    //entityId = "town_EW1";
                    entityId = "player_settlement_town_1";
                    GameEntity gameEntity = GameEntity.Instantiate(____scene, entityId, true);
                    if (gameEntity != null)
                    {
                        Vec3 vec3 = new Vec3(position.x, position.y, 0f, -1f)
                        {
                            z = ____scene.GetGroundHeightAtPosition(position.ToVec3(0f), BodyFlags.CommonCollisionExcludeFlags)
                        };
                        gameEntity.SetLocalPosition(vec3);
                    }

                    return false;
                }
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
            return true;
        }

    }
}
