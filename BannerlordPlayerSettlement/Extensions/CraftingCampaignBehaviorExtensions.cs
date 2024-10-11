using System;
using System.Collections.Generic;
using System.Reflection;

using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

using static TaleWorlds.CampaignSystem.CampaignBehaviors.CraftingCampaignBehavior;

namespace BannerlordPlayerSettlement.Extensions
{
    public static class CraftingCampaignBehaviorExtensions
    {
        static FieldInfo _craftingOrdersField = AccessTools.Field(typeof(CraftingCampaignBehavior), "_craftingOrders");

        public static bool AddTown(this CraftingCampaignBehavior craftingCampaignBehavior, Town town, out Dictionary<Town, CraftingOrderSlots>? _craftingOrders)
        {
            _craftingOrders = null;
            try
            {
                _craftingOrders = _craftingOrdersField.GetValue(craftingCampaignBehavior) as Dictionary<Town, CraftingOrderSlots>;
                if (_craftingOrders == null)
                {
                    return false;
                }


                if (_craftingOrders.ContainsKey(town))
                {
                    return true;
                }

                _craftingOrders[town] = new CraftingOrderSlots();

                MBList<Hero> mBList = new MBList<Hero>();
                Settlement settlement = town.Settlement;
                mBList.AddRange(settlement.HeroesWithoutParty);
                foreach (MobileParty party in settlement.Parties)
                {
                    if (party.LeaderHero == null || party.IsMainParty)
                    {
                        continue;
                    }
                    mBList.Add(party.LeaderHero);
                }
                if (mBList.Count > 0)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        if (craftingCampaignBehavior.CraftingOrders[settlement.Town].GetAvailableSlot() > -1)
                        {
                            craftingCampaignBehavior.CreateTownOrder(mBList.GetRandomElement<Hero>(), i);
                        }
                    }
                }
                mBList.Clear();

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}
