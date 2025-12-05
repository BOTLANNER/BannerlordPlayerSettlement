
using System.Collections.Generic;
using System.Linq;

using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using Helpers;

using SandBox;

using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(TownManagementVM))]
    public static class TownManagementVMPatch {

        [HarmonyPrefix]
        [HarmonyPatch(nameof(OnProjectSelectionDone))]
        public static bool OnProjectSelectionDone(ref TownManagementVM __instance, ref Settlement ____settlement)
        {
            try
            {
                if (__instance.ProjectSelection.CurrentDailyDefault == null)
                {
                    __instance.ProjectSelection.CurrentDailyDefault = __instance.ProjectSelection.DailyDefaultList.FirstOrDefault();
                }


                if (__instance.ProjectSelection.CurrentDailyDefault == null)
                {
                    LogManager.Log.NotifyBad($"[TownManagementVM] (ProjectSelection.CurrentDailyDefault) is null!");
                }

                //List<Building> localDevelopmentList = __instance.ProjectSelection.LocalDevelopmentList;
                //Building building = __instance.ProjectSelection.CurrentDailyDefault.Building;
                //if (localDevelopmentList != null)
                //{
                //    BuildingHelper.ChangeCurrentBuildingQueue(localDevelopmentList, ____settlement.Town);
                //}
                //if (building != ____settlement.Town.Buildings.FirstOrDefault<Building>((Building k) => k.IsCurrentlyDefault) && building != null)
                //{
                //    BuildingHelper.ChangeDefaultBuilding(building, ____settlement.Town);
                //}
                //__instance.RefreshCurrentDevelopment(ref ____settlement);

                //return false;
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }

            return true;
        }


        //public static void RefreshCurrentDevelopment(this TownManagementVM __instance, ref Settlement ____settlement)
        //{
        //    if (____settlement.Town.CurrentBuilding != null)
        //    {
        //        __instance.IsCurrentProjectDaily = ____settlement.Town.CurrentBuilding.BuildingType.IsDailyProject;
        //        if (!__instance.IsCurrentProjectDaily)
        //        {
        //            __instance.CurrentProjectProgress = (int) (BuildingHelper.GetProgressOfBuilding(__instance.ProjectSelection.CurrentSelectedProject.Building, ____settlement.Town) * 100f);
        //            __instance.ProjectSelection.CurrentSelectedProject.RefreshProductionText();
        //        }
        //    }
        //}
    }
}
