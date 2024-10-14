
using System;
using System.Linq;

using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Saves;
using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using SandBox;

using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(MapScene))]
    public static class MapScenePatch
    {

        [HarmonyPrefix]
        [HarmonyPatch(nameof(AddNewEntityToMapScene))]
        public static bool AddNewEntityToMapScene(ref MapScene __instance, ref Scene ____scene, ref string entityId, ref Vec2 position)
        {
            try
            {
                PlayerSettlementItem? playerSettlementItem = null;
                if (entityId?.IsPlayerBuiltStringId(out playerSettlementItem)  ?? false)
                {
                    if (entityId != null && entityId.StartsWith("player_settlement_town_"))
                    {
                        try
                        {
                            var x = entityId.Replace("player_settlement_town_", "").Split('_')[0];
                            if (int.TryParse(x, out int item) && item > (35))
                            {
                                entityId = entityId.Contains("village") ? $"player_settlement_town_1_village_{int.Parse(entityId.Split('_').Last())}" : "player_settlement_town_1";
                            }
                        }
                        catch (Exception) { /* Backward compat. No logging, this WILL get hit */ }
                    }

                    string prefabId = playerSettlementItem?.PrefabId ?? entityId!;
                    var entity = __instance.AddPrefabEntityToMapScene(ref ____scene, ref entityId!, ref position, ref prefabId!);
                    if (entity != null)
                    {
                        return false;
                    }
                }
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }
            return true;
        }

        public static GameEntity? AddPrefabEntityToMapScene(this IMapScene __instance, ref Scene ____scene, ref string entityId, ref Vec2 position, ref string prefabId)
        {
            try
            {
                GameEntity gameEntity = GameEntity.Instantiate(____scene, prefabId, true);
                if (gameEntity != null)
                {
                    if (entityId != prefabId)
                    {
                        gameEntity.Name = entityId;
                    }
                    Vec3 vec3 = new Vec3(position.x, position.y, 0f, -1f)
                    {
                        z = ____scene.GetGroundHeightAtPosition(position.ToVec3(0f), BodyFlags.CommonCollisionExcludeFlags)
                    };
                    gameEntity.SetLocalPosition(vec3);

                    return gameEntity;
                }
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }
            return null;
        }

    }
}
