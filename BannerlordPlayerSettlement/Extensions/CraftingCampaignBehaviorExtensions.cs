using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

using static TaleWorlds.CampaignSystem.CampaignBehaviors.CraftingCampaignBehavior;

namespace BannerlordPlayerSettlement.Extensions
{
    public static class CraftingCampaignBehaviorExtensions
    {
        static FieldInfo _craftingOrdersField = AccessTools.Field(typeof(CraftingCampaignBehavior), "_craftingOrders");

        public static bool AddTown(this CraftingCampaignBehavior craftingCampaignBehavior, Town town, out Dictionary<Town, CraftingOrderSlots> _craftingOrders)
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
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}
