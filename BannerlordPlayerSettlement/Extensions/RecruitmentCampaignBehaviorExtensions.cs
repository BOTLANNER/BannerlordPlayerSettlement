using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace BannerlordPlayerSettlement.Extensions
{
    public static class RecruitmentCampaignBehaviorExtensions
    {
        static FastInvokeHandler UpdateCurrentMercenaryTroopAndCountMethod = MethodInvoker.GetHandler(AccessTools.Method(typeof(RecruitmentCampaignBehavior), "UpdateCurrentMercenaryTroopAndCount"));
        static FastInvokeHandler UpdateVolunteersOfNotablesInSettlementMethod = MethodInvoker.GetHandler(AccessTools.Method(typeof(RecruitmentCampaignBehavior), "UpdateVolunteersOfNotablesInSettlement"));

        public static void NewSettlementBuilt(this RecruitmentCampaignBehavior recruitmentCampaignBehavior, Settlement settlement)
        {
            if (settlement.IsTown)
            {
                UpdateCurrentMercenaryTroopAndCountMethod.Invoke(recruitmentCampaignBehavior,settlement.Town, true);
            }
            UpdateVolunteersOfNotablesInSettlementMethod.Invoke(recruitmentCampaignBehavior, settlement);
        }
    }
}
