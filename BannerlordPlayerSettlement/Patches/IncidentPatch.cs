
using System;

using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using TaleWorlds.CampaignSystem.Incidents;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(Incident))]
    public static class IncidentPatch
    {

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(Incident.CanIncidentBeInvoked))]
        public static Exception FixCanIncidentBeInvoked(object __exception, ref Incident __instance, ref bool __result)
        {
            if (__exception != null)
            {
                var e = __exception;
                if (e != null)
                {
                    if (e is Exception ex)
                    {

                        LogManager.Log.NotifyBad(ex);
                    }
                    else
                    {

                        LogManager.Log.NotifyBad(e.ToString());
                    }

                    __result = false;
                }
            }
            return null;
        }
    }
}
