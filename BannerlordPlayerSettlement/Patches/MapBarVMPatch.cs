using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.UI;

using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
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
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
        }
    }
}
