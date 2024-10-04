using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using BannerlordPlayerSettlement.Behaviours;

using HarmonyLib;

using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Extensions
{
    public static class SettlementExtensions
    {
        static MethodInfo Position2DSetter = AccessTools.Property(typeof(Settlement), "Position2D").SetMethod;
        static MethodInfo GatePositionSetter = AccessTools.Property(typeof(Settlement), "GatePosition").SetMethod;
        public static void SetPosition2D(this Settlement settlement, Vec2 position, Vec2? gatePosition)
        {
            Position2DSetter.Invoke(settlement, new object[] { position });
            if (gatePosition != null)
            {
                GatePositionSetter.Invoke(settlement, new object[] { gatePosition });
            }
        }

        static MethodInfo BoundSetter = AccessTools.Property(typeof(Village), nameof(Village.Bound)).SetMethod;

        public static void SetBound(this Settlement settlement, Settlement boundTarget)
        {
            settlement.Village.SetBound(boundTarget);
        }

        public static void SetBound(this Village village, Settlement boundTarget)
        {
            BoundSetter.Invoke(village, new object[] { boundTarget });
        }

        public static bool IsPlayerBuilt(this Settlement? settlement)
        {
            return settlement?.StringId?.IsPlayerBuiltStringId() ?? false;
        }

        public static bool IsPlayerBuiltStringId(this string? stringId)
        {
            if (string.IsNullOrEmpty(stringId))
            {
                return false;
            }

            return stringId!.StartsWith("player_settlement_town_") || stringId!.StartsWith("player_settlement_castle_");
        }
    }
}
