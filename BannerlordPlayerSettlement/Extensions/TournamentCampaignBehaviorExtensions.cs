using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;

namespace BannerlordPlayerSettlement.Extensions
{
    public static class TournamentCampaignBehaviorExtensions
    {
        static FastInvokeHandler ConsiderStartOrEndTournamentMethod = MethodInvoker.GetHandler(AccessTools.Method(typeof(TournamentCampaignBehavior), "ConsiderStartOrEndTournament"));
        public static void NewTownBuilt(this TournamentCampaignBehavior tournamentCampaignBehavior, Town town)
        {
            ConsiderStartOrEndTournamentMethod.Invoke(tournamentCampaignBehavior, town);
        }
    }
}
