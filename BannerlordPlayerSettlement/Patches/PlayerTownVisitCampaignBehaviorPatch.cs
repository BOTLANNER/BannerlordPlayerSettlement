
using System.Linq;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Saves;

using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior))]
    public static class PlayerTownVisitCampaignBehaviorPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(game_menu_town_on_init))]
        public static bool game_menu_town_on_init(MenuCallbackArgs args)
        {
            try
            {
                if (Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsPlayerBuilt())
                {
                    var town = PlayerSettlementInfo.Instance?.Towns?.FirstOrDefault(t => t.Settlement == Settlement.CurrentSettlement);

                    if (Main.Settings!.InstantBuild)
                    {
                        return true;
                    }

                    if (town?.BuildEnd.IsFuture ?? true)
                    {
                        Campaign.Current.CurrentMenuContext.SwitchToMenu(PlayerSettlementBehaviour.PlayerSettlementUnderConstructionMenu);
                        return false;
                    }
                }
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(game_menu_village_on_init))]
        public static bool game_menu_village_on_init(MenuCallbackArgs args)
        {
            try
            {
                if (Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsPlayerBuilt())
                {
                    if (Main.Settings!.InstantBuild)
                    {
                        return true;
                    }

                    var village = PlayerSettlementInfo.Instance?.PlayerVillages?.FirstOrDefault(v => v.Settlement == Settlement.CurrentSettlement) ??
                                  PlayerSettlementInfo.Instance?.Towns?.SelectMany(t => t.Villages)?.FirstOrDefault(v => v.Settlement == Settlement.CurrentSettlement) ??
                                  PlayerSettlementInfo.Instance?.Castles?.SelectMany(t => t.Villages)?.FirstOrDefault(v => v.Settlement == Settlement.CurrentSettlement);

                    if (village?.BuildEnd.IsFuture ?? true)
                    {
                        Campaign.Current.CurrentMenuContext.SwitchToMenu(PlayerSettlementBehaviour.PlayerSettlementUnderConstructionMenu);
                        return false;
                    }
                }
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(game_menu_castle_on_init))]
        public static bool game_menu_castle_on_init(MenuCallbackArgs args)
        {
            try
            {
                if (Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsPlayerBuilt())
                {
                    var castle = PlayerSettlementInfo.Instance?.Castles?.FirstOrDefault(t => t.Settlement == Settlement.CurrentSettlement);

                    if (Main.Settings!.InstantBuild)
                    {
                        return true;
                    }

                    if (castle?.BuildEnd.IsFuture ?? true)
                    {
                        Campaign.Current.CurrentMenuContext.SwitchToMenu(PlayerSettlementBehaviour.PlayerSettlementUnderConstructionMenu);
                        return false;
                    }
                }
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
            return true;
        }
    }
}
