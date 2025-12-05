
using System;
using System.Collections.Generic;
using System.Linq;

using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using SandBox.Missions.MissionLogics.Towns;
using SandBox.Objects.AreaMarkers;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(WorkshopMissionHandler))]
    public static class WorkshopMissionHandlerPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(InitShopSigns))]
        public static bool InitShopSigns(ref WorkshopMissionHandler __instance, ref Settlement ____settlement, ref List<Tuple<Workshop, GameEntity>> ____workshopSignEntities)
        {
            try
            {
                Settlement? settlement = PlayerEncounter.LocationEncounter?.Settlement;
                bool isPlayerSettlement =
                (
                    (settlement.IsPlayerBuilt()) ||
                    (____settlement.IsPlayerBuilt())
                );
                if (!isPlayerSettlement)
                {
                    return true;
                }

                if (Campaign.Current.GameMode == CampaignGameMode.Campaign && ____settlement != null && ____settlement.IsTown)
                {
                    List<GameEntity> list = __instance.Mission.Scene.FindEntitiesWithTag("shop_sign").ToList<GameEntity>();
                Label0_patch:
                    foreach (WorkshopAreaMarker workshopAreaMarker in __instance.Mission.ActiveMissionObjects.FindAllWithType<WorkshopAreaMarker>().ToList<WorkshopAreaMarker>())
                    {
                        if (____settlement.Town.Workshops == null || ____settlement.Town.Workshops.Length == 0)
                        {
                            continue;
                        }

                        Workshop workshops = ____settlement.Town.Workshops[workshopAreaMarker.AreaIndex];
                        if (!____workshopSignEntities.All<Tuple<Workshop, GameEntity>>((Tuple<Workshop, GameEntity> x) => x.Item1 != workshops))
                        {
                            continue;
                        }
                        int num = 0;
                        while (num < list.Count)
                        {
                            GameEntity item = list[num];
                            if (!workshopAreaMarker.IsPositionInRange(item.GlobalPosition))
                            {
                                num++;
                            }
                            else
                            {
                                ____workshopSignEntities.Add(new Tuple<Workshop, GameEntity>(workshops, item));
                                list.RemoveAt(num);
                                goto Label0_patch;
                            }
                        }
                    }
                    foreach (Tuple<Workshop, GameEntity> _workshopSignEntity in ____workshopSignEntities)
                    {
                        GameEntity item2 = _workshopSignEntity.Item2;
                        WorkshopType workshopType = _workshopSignEntity.Item1.WorkshopType;
                        item2.ClearComponents();
                        item2.AddMultiMesh(MetaMesh.GetCopy((workshopType != null ? workshopType.SignMeshName : "shop_sign_merchantavailable"), true, false), true);
                    }
                }

                return false;
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }

            return true;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(InitShopSigns))]
        public static Exception? FixInitShopSigns(Exception? __exception, ref WorkshopMissionHandler __instance, ref Settlement ____settlement)
        {
            if (__exception != null)
            {
                Settlement? settlement = PlayerEncounter.LocationEncounter?.Settlement;
                bool isPlayerSettlement =
                (
                    (settlement.IsPlayerBuilt()) ||
                    (____settlement.IsPlayerBuilt())
                );
                if (!isPlayerSettlement)
                {
                    return __exception;
                }
                var e = __exception;
                LogManager.Log.NotifyBad(e);
            }
            return null;
        }
    }
}