//using System;

//using BannerlordPlayerSettlement.UI;

//using HarmonyLib;

//using TaleWorlds.CampaignSystem.ComponentInterfaces;
//using TaleWorlds.CampaignSystem.GameComponents;
//using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
//using TaleWorlds.Library;

//namespace BannerlordPlayerSettlement.Patches
//{
//    [HarmonyPatch(typeof(DefaultMapWeatherModel))]
//    public static class DefaultMapWeatherModelPatch
//    {
//        [HarmonyFinalizer]
//        [HarmonyPatch(nameof(DefaultMapWeatherModel.GetWeatherEventInPosition))]
//        public static Exception? GetWeatherEventInPosition(ref Exception __exception, ref MapWeatherModel.WeatherEvent __result, ref DefaultMapWeatherModel __instance, Vec2 pos)
//        {
//            if (__exception != null)
//            {
//                __result = MapWeatherModel.WeatherEvent.Clear;

//                var e = __exception;
//                TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace);
//                Debug.WriteDebugLineOnScreen(e.ToString());
//                Debug.SetCrashReportCustomString(e.Message);
//                Debug.SetCrashReportCustomStack(e.StackTrace);
//            }
//            return null;
//        }
//    }
//}
