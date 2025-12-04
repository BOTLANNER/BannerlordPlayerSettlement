using System.Linq;
using System.Reflection;

using BannerlordPlayerSettlement.Saves;

using HarmonyLib;

using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Extensions
{
    public static class SettlementExtensions
    {
        //static MethodInfo Position2DSetter = AccessTools.Property(typeof(Settlement), "Position2D").SetMethod;
        //static MethodInfo GatePositionSetter = AccessTools.Property(typeof(Settlement), "GatePosition").SetMethod;
        //public static void SetPosition2D(this Settlement settlement, Vec2 position, Vec2? gatePosition)
        //{
        //    Position2DSetter.Invoke(settlement, new object[] { position });
        //    if (gatePosition != null)
        //    {
        //        GatePositionSetter.Invoke(settlement, new object[] { gatePosition });
        //    }
        //}

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

        public static bool IsOverwritten(this Settlement? settlement, out OverwriteSettlementItem? overwriteSettlementItem)
        {
            overwriteSettlementItem = null;
            return settlement?.StringId?.IsOverwritten(out overwriteSettlementItem) ?? false;
        }

        public static bool IsPlayerBuilt(this Settlement? settlement, out PlayerSettlementItem? playerSettlementItem)
        {
            playerSettlementItem = null;
            return settlement?.StringId?.IsPlayerBuiltStringId(out playerSettlementItem) ?? false;
        }

        public static bool IsPlayerBuiltStringId(this string? stringId)
        {
            return stringId.IsPlayerBuiltStringId(out _);
        }

        public static bool IsPlayerBuiltStringId(this string? stringId, out PlayerSettlementItem? playerSettlementItem)
        {
            if (string.IsNullOrEmpty(stringId))
            {
                playerSettlementItem = null;
                return false;
            }

            if (PlayerSettlementInfo.Instance != null)
            {
                var isPlayerTown = PlayerSettlementInfo.Instance.Towns.FirstOrDefault(t => t.Settlement?.StringId == stringId || t.StringId == stringId);
                if (isPlayerTown != null)
                {
                    playerSettlementItem = isPlayerTown;
                    return true;
                }
                var isPlayerCastle = PlayerSettlementInfo.Instance.Castles.FirstOrDefault(t => t.Settlement?.StringId == stringId || t.StringId == stringId);
                if (isPlayerCastle != null)
                {
                    playerSettlementItem = isPlayerCastle;
                    return true;
                }
                var isPlayerVillage = (PlayerSettlementInfo.Instance.PlayerVillages?.FirstOrDefault(v => v.Settlement?.StringId == stringId || v.StringId == stringId)) ??
                                       PlayerSettlementInfo.Instance.Towns.SelectMany(t => t.Villages).FirstOrDefault(v => v.Settlement?.StringId == stringId || v.StringId == stringId) ??
                                       PlayerSettlementInfo.Instance.Castles.SelectMany(c => c.Villages).FirstOrDefault(v => v.Settlement?.StringId == stringId || v.StringId == stringId);
                if (isPlayerVillage != null)
                {
                    playerSettlementItem = isPlayerVillage;
                    return true;
                }
            }

            playerSettlementItem = null;
            return stringId!.StartsWith("player_settlement_town_") || stringId!.StartsWith("player_settlement_castle_");
        }

        public static bool IsOverwritten(this string? stringId, out OverwriteSettlementItem? overwriteSettlementItem)
        {
            if (string.IsNullOrEmpty(stringId))
            {
                overwriteSettlementItem = null;
                return false;
            }

            if (PlayerSettlementInfo.Instance != null)
            {
                if (PlayerSettlementInfo.Instance.OverwriteSettlements == null)
                {
                    PlayerSettlementInfo.Instance.OverwriteSettlements = new();
                }
                var isOverwrite = PlayerSettlementInfo.Instance.OverwriteSettlements.FirstOrDefault(t => t.Settlement?.StringId == stringId || t.StringId == stringId);
                if (isOverwrite != null)
                {
                    overwriteSettlementItem = isOverwrite;
                    return true;
                }
            }

            overwriteSettlementItem = null;
            return false;
        }

        public static SettlementType GetSettlementType(this Settlement? settlement)
        {
            if (settlement == null)
            {
                return SettlementType.None;
            }

            if (settlement.IsVillage)
            {
                return SettlementType.Village;
            }

            if (settlement.IsCastle)
            {
                return SettlementType.Castle;
            }

            if (settlement.IsTown)
            {
                return SettlementType.Town;
            }

            return SettlementType.None;
        }
    }
}
