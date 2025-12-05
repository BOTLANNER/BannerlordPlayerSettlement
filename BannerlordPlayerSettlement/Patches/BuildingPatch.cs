
using System.Reflection;

using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(Building))]
    public static class BuildingPatch
    {
        //static FieldInfo _currentLevelField = AccessTools.Field(typeof(Building), "_currentLevel");


        [HarmonyPostfix]
        [HarmonyPatch(nameof(Building.CurrentLevel), MethodType.Getter)]
        public static void GetCurrentLevel(ref Building __instance, ref int __result)
        {
            try
            {
                if (__result < 1)
                {
                    __result = 1;
                    __instance.CurrentLevel = __result;
                }
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }
        }
    }
}
