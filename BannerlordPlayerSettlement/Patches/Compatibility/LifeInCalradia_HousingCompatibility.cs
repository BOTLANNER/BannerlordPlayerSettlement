using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Patches.Compatibility.Interfaces;
using BannerlordPlayerSettlement.Saves;

using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Patches.Compatibility
{
    // LifeInCalradia_Housing
    public class LifeInCalradia_HousingCompatibility : ICompatibilityPatch
    {
        public bool IsEnabled => assembly != null && behaviorType != null;

        private Assembly? assembly;
        private Type? behaviorType;

        public void AddBehaviors(CampaignGameStarter gameInitializer)
        {
        }

        public void PatchAfterMenus(Harmony harmony)
        {
        }

        public void PatchSubmoduleLoad(Harmony harmony)
        {
            assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWith("LifeInCalradia_Housing, ", StringComparison.InvariantCultureIgnoreCase));

            if (assembly != null)
            {
                behaviorType = assembly.GetType("LifeInCalradia_Housing.Behaviors.HousingBehavior", false, true);
                if (behaviorType != null)
                {
                    harmony.Patch(AccessTools.Method(behaviorType, "GetOrCreateSettlementHousing"), prefix: new HarmonyMethod(typeof(LifeInCalradia_HousingCompatibility), nameof(GetOrCreateSettlementHousing)));
                }
            }
        }

        private static bool GetOrCreateSettlementHousing(ref object __result, object __instance, Settlement settlement)
        {
            if (settlement != null && settlement.IsPlayerBuilt() && settlement.Notables.Count == 0)
            {
                __result = null;
                return false;
            }
            return true;
        }
    }
}
