using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
    }
}
