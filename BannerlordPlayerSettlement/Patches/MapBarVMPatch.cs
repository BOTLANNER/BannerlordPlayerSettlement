using BannerlordPlayerSettlement.UI.Viewmodels;
using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(MapBarVM))]
    public static class MapBarVMPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(MapBarVM.Tick))]
        public static void Tick(ref MapBarVM __instance, float dt)
        {
            try
            {
                MapBarExtensionVM.Current?.Tick(dt);
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }
        }
    }
}
