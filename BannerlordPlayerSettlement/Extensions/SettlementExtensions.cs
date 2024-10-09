using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Saves;

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

            if (PlayerSettlementInfo.Instance != null)
            {
                var isPlayerTown = PlayerSettlementInfo.Instance.Towns.Any(t => t.Settlement?.StringId == stringId || t.StringId == stringId);
                if (isPlayerTown)
                {
                    return true;
                }
                var isPlayerCastle = PlayerSettlementInfo.Instance.Towns.Any(t => t.Settlement?.StringId == stringId || t.StringId == stringId);
                if (isPlayerCastle)
                {
                    return true;
                }
                var isPlayerVillage = PlayerSettlementInfo.Instance.Towns.SelectMany(t => t.Villages).Any(v => v.Settlement?.StringId == stringId || v.StringId == stringId) ||
                                      PlayerSettlementInfo.Instance.Castles.SelectMany(c => c.Villages).Any(v => v.Settlement?.StringId == stringId || v.StringId == stringId);
                if (isPlayerVillage)
                {
                    return true;
                }
            }

            return stringId!.StartsWith("player_settlement_town_") || stringId!.StartsWith("player_settlement_castle_");
        }
    }
}
