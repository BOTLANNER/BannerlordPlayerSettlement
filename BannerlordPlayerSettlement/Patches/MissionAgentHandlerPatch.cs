
using System;
using System.Collections.Generic;
using System.Linq;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Extensions;

using HarmonyLib;

using SandBox.Missions.AgentBehaviors;
using SandBox.Missions.MissionLogics;
using SandBox.Objects.AreaMarkers;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Objects;
using TaleWorlds.MountAndBlade.Source.Objects;

namespace BannerlordPlayerSettlement.Patches
{

    [HarmonyPatch(typeof(MissionAgentHandler))]
    public static class MissionAgentHandlerPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(GetAllProps))]
        public static bool GetAllProps(ref MissionAgentHandler __instance, ref int ____disabledFaceId, ref int ____disabledFaceIdForAnimals, ref Dictionary<string, List<UsableMachine>> ____usablePoints)
        {
            try
            {
                Settlement settlement = PlayerEncounter.LocationEncounter.Settlement;
                bool isPlayerSettlement = (settlement.IsPlayerBuilt());
                if (!isPlayerSettlement)
                {
                    return true;
                }
                string str;
                GameEntity gameEntity = __instance.Mission.Scene.FindEntityWithTag("navigation_mesh_deactivator");
                if (gameEntity != null)
                {
                    NavigationMeshDeactivator firstScriptOfType = gameEntity.GetFirstScriptOfType<NavigationMeshDeactivator>();
                    ____disabledFaceId = firstScriptOfType.DisableFaceWithId;
                    ____disabledFaceIdForAnimals = firstScriptOfType.DisableFaceWithIdForAnimals;
                }
                ____usablePoints.Clear();
                foreach (UsableMachine usableMachine in __instance.Mission.MissionObjects.FindAllWithType<UsableMachine>())
                {
                    string[] tags = usableMachine.GameEntity.Tags;
                    for (int i = 0; i < (int) tags.Length; i++)
                    {
                        string str1 = tags[i];
                        if (!____usablePoints.ContainsKey(str1))
                        {
                            ____usablePoints.Add(str1, new List<UsableMachine>());
                        }
                        ____usablePoints[str1].Add(usableMachine);
                    }
                }
                if (Settlement.CurrentSettlement.IsTown || Settlement.CurrentSettlement.IsVillage)
                {
                    foreach (AreaMarker list in __instance.Mission.ActiveMissionObjects.FindAllWithType<AreaMarker>().ToList<AreaMarker>())
                    {
                        string tag = list.Tag;
                        if (tag == null)
                        {
                            continue;
                        }

                        AreaMarker areaMarker = list;
                        if (list.Tag.Contains("workshop"))
                        {
                            str = "unaffected_by_area";
                        }
                        else
                        {
                            str = null;
                        }
                        List<UsableMachine> usableMachinesInRange = areaMarker.GetUsableMachinesInRange(str);
                        if (!____usablePoints.ContainsKey(tag))
                        {
                            ____usablePoints.Add(tag, new List<UsableMachine>());
                        }
                        foreach (UsableMachine usableMachine1 in usableMachinesInRange)
                        {
                            foreach (KeyValuePair<string, List<UsableMachine>> _usablePoint in ____usablePoints)
                            {
                                if (!_usablePoint.Value.Contains(usableMachine1))
                                {
                                    continue;
                                }
                                _usablePoint.Value.Remove(usableMachine1);
                            }
                            if (!usableMachine1.GameEntity.HasTag("hold_tag_always"))
                            {
                                foreach (UsableMachine usableMachine2 in usableMachinesInRange)
                                {
                                    if (usableMachine2.GameEntity.HasTag(tag))
                                    {
                                        continue;
                                    }
                                    usableMachine2.GameEntity.AddTag(tag);
                                }
                            }
                            else
                            {
                                string str2 = String.Concat(usableMachine1.GameEntity.Tags[0], "_", list.Tag);
                                usableMachine1.GameEntity.AddTag(str2);
                                if (____usablePoints.ContainsKey(str2))
                                {
                                    ____usablePoints[str2].Add(usableMachine1);
                                }
                                else
                                {
                                    ____usablePoints.Add(str2, new List<UsableMachine>());
                                    ____usablePoints[str2].Add(usableMachine1);
                                }
                            }
                        }
                        if (____usablePoints.ContainsKey(tag))
                        {
                            var usp = ____usablePoints;
                            usableMachinesInRange.RemoveAll((UsableMachine x) => usp[tag].Contains(x));
                            if (usableMachinesInRange.Count > 0)
                            {
                                ____usablePoints[tag].AddRange(usableMachinesInRange);
                            }
                        }
                        foreach (UsableMachine usableMachinesWithTagInRange in list.GetUsableMachinesWithTagInRange("unaffected_by_area"))
                        {
                            string tags1 = usableMachinesWithTagInRange.GameEntity.Tags[0];
                            foreach (KeyValuePair<string, List<UsableMachine>> keyValuePair in ____usablePoints)
                            {
                                if (!keyValuePair.Value.Contains(usableMachinesWithTagInRange))
                                {
                                    continue;
                                }
                                keyValuePair.Value.Remove(usableMachinesWithTagInRange);
                            }
                            if (!____usablePoints.ContainsKey(tags1))
                            {
                                ____usablePoints.Add(tags1, new List<UsableMachine>());
                                ____usablePoints[tags1].Add(usableMachinesWithTagInRange);
                            }
                            else
                            {
                                ____usablePoints[tags1].Add(usableMachinesWithTagInRange);
                            }
                        }
                    }
                }
                __instance.DisableUnavailableWaypoints();
                __instance.RemoveDeactivatedUsablePlacesFromList();

                return false;
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }

            return true;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(GetAllProps))]
        public static Exception? FixGetAllProps(ref Exception __exception, ref MissionAgentHandler __instance)
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
                TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace);
                Debug.WriteDebugLineOnScreen(e.ToString());
                Debug.SetCrashReportCustomString(e.Message);
                Debug.SetCrashReportCustomStack(e.StackTrace);
            }
            return null;
        }
    }
}