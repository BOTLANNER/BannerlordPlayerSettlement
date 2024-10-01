
using BannerlordPlayerSettlement.Behaviours;

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
                if (name == PlayerSettlementBehaviour.PlayerSettlementIdentifier)
                {
                    //name = "town_EW1";
                    name = "player_settlement_town_1";
                }
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
            return true;
        }
    }
}
