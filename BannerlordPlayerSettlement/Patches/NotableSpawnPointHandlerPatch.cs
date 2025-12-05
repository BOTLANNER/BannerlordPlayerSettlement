
using System;
using System.Collections.Generic;
using System.Linq;

using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using SandBox.Missions.AgentBehaviors;
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

    [HarmonyPatch(typeof(NotableSpawnPointHandler))]
    public static class NotableSpawnPointHandlerPatch
    {
        static readonly FastInvokeHandler FindAndSetChildInvoker = MethodInvoker.GetHandler(AccessTools.Method(typeof(NotableSpawnPointHandler), nameof(FindAndSetChild)));

        public static void FindAndSetChild(this NotableSpawnPointHandler notableSpawnPointHandler, GameEntity childGameEntity)
        {
            FindAndSetChildInvoker(notableSpawnPointHandler,childGameEntity );
        }

        static readonly FastInvokeHandler ActivateParentSetInsideWorkshopInvoker = MethodInvoker.GetHandler(AccessTools.Method(typeof(NotableSpawnPointHandler), nameof(ActivateParentSetInsideWorkshop)));

        public static void ActivateParentSetInsideWorkshop(this NotableSpawnPointHandler notableSpawnPointHandler, WorkshopAreaMarker areaMarker)
        {
            ActivateParentSetInsideWorkshopInvoker(notableSpawnPointHandler, areaMarker);
        }

        static readonly FastInvokeHandler ActivateParentSetOutsideWorkshopInvoker = MethodInvoker.GetHandler(AccessTools.Method(typeof(NotableSpawnPointHandler), nameof(ActivateParentSetOutsideWorkshop)));

        public static void ActivateParentSetOutsideWorkshop(this NotableSpawnPointHandler notableSpawnPointHandler)
        {
            ActivateParentSetOutsideWorkshopInvoker(notableSpawnPointHandler);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(OnBehaviorInitialize))]
        public static bool OnBehaviorInitialize(ref NotableSpawnPointHandler __instance, ref List<Hero> ____workshopAssignedHeroes,ref int ____merchantNotableCount, ref int ____gangLeaderNotableCount, ref int ____preacherNotableCount, ref int ____artisanNotableCount, ref int ____ruralNotableCount)
        {
            try
            {
                Settlement settlement = PlayerEncounter.LocationEncounter.Settlement;
                bool isPlayerSettlement = (settlement.IsPlayerBuilt());
                if (!isPlayerSettlement)
                {
                    return true;
                }

                List<GameEntity> list = Mission.Current.Scene.FindEntitiesWithTag("sp_notables_parent").ToList();
                ____workshopAssignedHeroes = new List<Hero>();
                foreach (Hero notable in settlement!.Notables)
                {
                    if (notable.IsGangLeader)
                    {
                        ____gangLeaderNotableCount++;
                    }
                    else if (notable.IsPreacher)
                    {
                        ____preacherNotableCount++;
                    }
                    else if (notable.IsArtisan)
                    {
                        ____artisanNotableCount++;
                    }
                    else if (notable.IsRuralNotable || notable.IsHeadman)
                    {
                        ____ruralNotableCount++;
                    }
                    else if (notable.IsMerchant)
                    {
                        ____merchantNotableCount++;
                    }
                }
                foreach (GameEntity item in list.ToList())
                {
                    foreach (GameEntity child in item.GetChildren())
                    {
                       __instance.FindAndSetChild(child);
                    }
                    foreach (WorkshopAreaMarker item2 in (from x in __instance.Mission.ActiveMissionObjects.FindAllWithType<WorkshopAreaMarker>().ToList()
                                                          orderby x.AreaIndex
                                                          select x).ToList())
                    {
                        if (item2.IsPositionInRange(item.GlobalPosition) && item2.GetWorkshop() != null && item2.GetWorkshop().Owner.OwnedWorkshops.First((Workshop x) => !x.WorkshopType.IsHidden).Tag == item2.Tag)
                        {
                           __instance.ActivateParentSetInsideWorkshop(item2);
                            list.Remove(item);
                            break;
                        }
                    }
                }
                foreach (GameEntity item3 in list)
                {
                    foreach (GameEntity child2 in item3.GetChildren())
                    {
                       __instance.FindAndSetChild(child2);
                    }
                    __instance.ActivateParentSetOutsideWorkshop();
                }

                return false;
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }

            return true;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(NotableSpawnPointHandler.OnBehaviorInitialize))]
        public static Exception? FixOnBehaviorInitialize(Exception? __exception, ref NotableSpawnPointHandler __instance)
        {
            if (__exception != null)
            {
                Settlement settlement = PlayerEncounter.LocationEncounter.Settlement;
                bool isPlayerSettlement = (settlement.IsPlayerBuilt());
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