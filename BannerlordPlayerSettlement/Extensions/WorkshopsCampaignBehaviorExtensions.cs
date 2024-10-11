using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace BannerlordPlayerSettlement.Extensions
{
    public static class WorkshopsCampaignBehaviorExtensions
    {
        static FastInvokeHandler BuildArtisanWorkshopMethod = MethodInvoker.GetHandler(AccessTools.Method(typeof(WorkshopsCampaignBehavior), "BuildArtisanWorkshop"));
        static FastInvokeHandler BuildWorkshopForHeroAtGameStartMethod = MethodInvoker.GetHandler(AccessTools.Method(typeof(WorkshopsCampaignBehavior), "BuildWorkshopForHeroAtGameStart"));
        public static void NewTownBuilt(this WorkshopsCampaignBehavior behavior, Town town)
        {
            town.InitializeWorkshops(Campaign.Current.Models.WorkshopModel.DefaultWorkshopCountInSettlement);
            BuildArtisanWorkshopMethod.Invoke(behavior,  town );
            for (int i = 1; i < (int) town.Workshops.Length; i++)
            {
                Hero notableOwnerForWorkshop = Campaign.Current.Models.WorkshopModel.GetNotableOwnerForWorkshop(town.Workshops[i]);
                if (notableOwnerForWorkshop == null)
                {
                    continue;
                }
                BuildWorkshopForHeroAtGameStartMethod.Invoke(behavior, notableOwnerForWorkshop);
            }
        }
    }
}
