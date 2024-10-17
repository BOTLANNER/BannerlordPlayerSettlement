using System.Collections.Generic;
using System.Linq;

using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.UI.Viewmodels;
using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using SandBox.ViewModelCollection.Nameplate;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Patches
{
    [HarmonyPatch(typeof(SettlementNameplatesVM))]
    public static class SettlementNameplatesVMPatch
    {

        [HarmonyPrefix]
        [HarmonyPatch(nameof(OnSettlementOwnerChanged))]
        private static bool OnSettlementOwnerChanged(ref SettlementNameplatesVM __instance, ref MBBindingList<SettlementNameplateVM> ____nameplates, Settlement settlement, bool openToClaim, Hero newOwner, Hero previousOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                SettlementNameplateVM settlementNameplateVM = __instance.Nameplates.FirstOrDefault<SettlementNameplateVM>((SettlementNameplateVM n) => n.Settlement == settlement);
                if (settlementNameplateVM != null)
                {
                    settlementNameplateVM.RefreshDynamicProperties(true);
                }
                else
                {
                }
                if (settlementNameplateVM != null)
                {
                    settlementNameplateVM.RefreshRelationStatus();
                }
                else
                {
                }
                foreach (Village boundVillage in settlement.BoundVillages)
                {
                    var list = __instance.Nameplates.Where<SettlementNameplateVM>((SettlementNameplateVM n) =>
                    {
                        if (!n.Settlement.IsVillage)
                        {
                            return false;
                        }
                        return n.Settlement.Village == boundVillage;
                    }).ToList();

                    SettlementNameplateVM settlementNameplateVM1;
                    if (list.Count > 1)
                    {
                        List<string> extraInfo = new()
                        {
                            $"Found {list.Count} settlements that share a village component!",
                            $"Village: {boundVillage}"
                        };
                        extraInfo.AddRange(list.Select(v => "\t" + v.Settlement.ToString()));
                        LogManager.EventTracer.Trace(extraInfo);
                        settlementNameplateVM1 = list.SingleOrDefault(v => v.Settlement == boundVillage.Settlement);
                    }
                    else
                    {
                        settlementNameplateVM1 = list.SingleOrDefault();
                    }
                    if (settlementNameplateVM1 != null)
                    {
                        settlementNameplateVM1.RefreshDynamicProperties(true);
                    }
                    else
                    {
                    }
                    if (settlementNameplateVM1 != null)
                    {
                        settlementNameplateVM1.RefreshRelationStatus();
                    }
                    else
                    {
                    }
                }
                if (detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByRebellion)
                {
                    SettlementNameplateVM settlementNameplateVM2 = __instance.Nameplates.FirstOrDefault<SettlementNameplateVM>((SettlementNameplateVM n) => n.Settlement == settlement);
                    if (settlementNameplateVM2 == null)
                    {
                        // return
                        return false;
                    }
                    settlementNameplateVM2.OnRebelliousClanFormed(newOwner.Clan);
                    // return
                    return false;
                }
                if (previousOwner != null && previousOwner.IsRebel)
                {
                    SettlementNameplateVM settlementNameplateVM3 = __instance.Nameplates.FirstOrDefault<SettlementNameplateVM>((SettlementNameplateVM n) => n.Settlement == settlement);
                    if (settlementNameplateVM3 == null)
                    {
                        // return
                        return false;
                    }
                    settlementNameplateVM3.OnRebelliousClanDisbanded(previousOwner.Clan);
                }

                return false;
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }
            return true;
        }
    }
}
