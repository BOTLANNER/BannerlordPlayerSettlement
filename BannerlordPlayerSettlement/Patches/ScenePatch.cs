
using System;
using System.Linq;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Extensions;

using HarmonyLib;

using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(Scene))]
    public static class ScenePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(GetCampaignEntityWithName))]
        public static bool GetCampaignEntityWithName(ref Scene __instance,ref string name)
        {
            try
            {
                if (name.IsPlayerBuiltStringId())
                {
                    if (name != null && name.StartsWith("player_settlement_town_"))
                    {
                        try
                        {
                            var x = name.Replace("player_settlement_town_", "").Split('_')[0];
                            var item = int.Parse(x);
                            if (item > (Settings.HardMaxCastles + Settings.HardMaxTowns + Settings.HardMaxVillagesPerCastle + Settings.HardMaxVillagesPerTown))
                            {
                                name = name.Contains("village") ? $"player_settlement_town_1_village_{int.Parse(name.Split('_').Last())}" : "player_settlement_town_1";
                            }
                        } catch(Exception) { }
                        
                    }
                }
                //if (name == PlayerSettlementBehaviour.PlayerSettlementIdentifier)
                //{
                //    //name = "town_EW1";
                //    name = "player_settlement_town_1";
                //}

                //var eId = name;
                //if (PlayerSettlementBehaviour.PlayerVillages.Any(pv => pv.ItemIdentifier == eId))
                //{
                //    //entityId = "town_EW1";
                //    var number = PlayerSettlementBehaviour.PlayerVillages.FindIndex(pv => pv.ItemIdentifier == eId) + 1;
                //    name = $"player_settlement_town_village_{number}";
                //}
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
            return true;
        }
    }
}
