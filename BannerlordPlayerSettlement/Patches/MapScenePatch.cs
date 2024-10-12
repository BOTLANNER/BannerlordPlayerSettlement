﻿
using System;
using System.Linq;

using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using SandBox;

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
                if (entityId?.IsPlayerBuiltStringId() ?? false)
                {
                    if (entityId != null && entityId.StartsWith("player_settlement_town_"))
                    {
                        try
                        {
                            var x = entityId.Replace("player_settlement_town_", "").Split('_')[0];
                            var item = int.Parse(x);
                            if (item > (Settings.HardMaxCastles + Settings.HardMaxTowns + Settings.HardMaxVillagesPerCastle + Settings.HardMaxVillagesPerTown))
                            {
                                entityId = entityId.Contains("village") ? $"player_settlement_town_1_village_{int.Parse(entityId.Split('_').Last())}" : "player_settlement_town_1";
                            }
                        }
                        catch (Exception) { /* Backward compat. No logging, this WILL get hit */ }
                    }


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
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }
            return true;
        }

    }
}
