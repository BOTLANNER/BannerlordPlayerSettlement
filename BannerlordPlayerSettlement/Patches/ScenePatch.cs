
using System;
using System.Linq;

using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Utils;

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
                            if (int.TryParse(x, out int item) && item > (35))
                            {
                                name = name.Contains("village") ? $"player_settlement_town_1_village_{int.Parse(name.Split('_').Last())}" : "player_settlement_town_1";
                            }
                        } catch(Exception) { /* Backward compat. This WILL get hit */ }
                        
                    }
                }
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }
            return true;
        }
    }
}
