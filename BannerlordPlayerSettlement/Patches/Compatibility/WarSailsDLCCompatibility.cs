using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Patches.Compatibility.Interfaces;
using BannerlordPlayerSettlement.Saves;
using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Patches.Compatibility
{
    // WarSailsDLC
    public class WarSailsDLCCompatibility : ICompatibilityPatch
    {
        public bool IsEnabled => Main.IsWarSails && assembly != null && ShipTradeCampaignBehaviorType != null;

        private Assembly? assembly;
        private Type? ShipTradeCampaignBehaviorType;
        private Type? NavalDLCShipCostModelType;

        public void AddBehaviors(CampaignGameStarter gameInitializer)
        {
            if (IsEnabled)
            {
                Behaviour campaignBehavior = new Behaviour
                {
                    Owner = this
                };
                gameInitializer.AddBehavior(campaignBehavior);
            }
        }

        public void PatchAfterMenus(Harmony harmony)
        {
        }

        public void PatchSubmoduleLoad(Harmony harmony)
        {
            if (!Main.IsWarSails)
            {
                return;
            }

            assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWith("NavalDLC, ", StringComparison.InvariantCultureIgnoreCase));

            if (assembly != null)
            {
                ShipTradeCampaignBehaviorType = assembly.GetType("NavalDLC.CampaignBehaviors.ShipTradeCampaignBehavior", false, true);
                if (ShipTradeCampaignBehaviorType != null)
                {
                    harmony.Patch(AccessTools.Method(ShipTradeCampaignBehaviorType, nameof(TryPurchasingShipFromTown)), prefix: new HarmonyMethod(typeof(WarSailsDLCCompatibility), nameof(TryPurchasingShipFromTown)), finalizer: new HarmonyMethod(typeof(WarSailsDLCCompatibility), nameof(FixExceptions)));

                }

                NavalDLCShipCostModelType = assembly.GetType("NavalDLC.GameComponents.NavalDLCShipCostModel", false, true);
                if (NavalDLCShipCostModelType != null)
                {
                    harmony.Patch(AccessTools.Method(NavalDLCShipCostModelType, nameof(GetShipTradeValue)), prefix: new HarmonyMethod(typeof(WarSailsDLCCompatibility), nameof(GetShipTradeValue)), finalizer: new HarmonyMethod(typeof(WarSailsDLCCompatibility), nameof(FixExceptions)));
                    harmony.Patch(AccessTools.Method(NavalDLCShipCostModelType, nameof(GetShipBaseValue)), prefix: new HarmonyMethod(typeof(WarSailsDLCCompatibility), nameof(GetShipBaseValue)), finalizer: new HarmonyMethod(typeof(WarSailsDLCCompatibility), nameof(FixExceptions)));

                }
            }
        }

        private static bool GetShipTradeValue(ShipCostModel __instance, Ship ship, PartyBase seller, PartyBase buyer)
        {
            if (ship?.Owner == null || ship?.Name == null)
            {
                return false;
            }
            return true;
        }

        private static bool GetShipBaseValue(Ship ship, bool applyAiDiscount, PartyBase owner)
        {
            if (ship?.Owner == null || ship?.Name == null)
            {
                return false;
            }
            return true;
        }

        public static Exception FixExceptions(object __exception, ref object __instance)
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
                }
            }
            return null;
        }

        private static bool TryPurchasingShipFromTown(object __instance, MobileParty mobileParty, Town town)
        {
            if (town.AvailableShips != null)
            {
                foreach (var ship in town.AvailableShips)
                {
                    if (ship.Owner == null)
                    {
                        // Cannot set owner here as it breaks the enumerator
                        //ship.Owner = town.Settlement.Party;
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        private static List<Settlement> CollectPlayerBuiltPorts()
        {
            var towns = new List<Settlement>();
            if (PlayerSettlementInfo.Instance == null)
            {
                return towns;
            }
            towns.AddRange((PlayerSettlementInfo.Instance.Towns?.Select(t => t.Settlement!) ?? new List<Settlement>()).Where(t => t.HasPort));
            towns.AddRange((PlayerSettlementInfo.Instance.OverwriteSettlements?.Select(t => t.Settlement!) ?? new List<Settlement>()).Where(t => t.IsTown && !t.IsCastle && t.HasPort));
            return towns.ToList();
        }

        public class Behaviour : CampaignBehaviorBase
        {
            public WarSailsDLCCompatibility? Owner = null;
            private bool HasLoaded { get; set; }

            private Type ShipProductionCampaignBehaviorType = null;
            private MethodInfo DailyTickTownMethod = null;

            private Assembly WarSailsAssembly
            {
                get
                {
                    return Owner?.assembly;
                }
                set
                {
                    if (Owner != null)
                    {
                        Owner.assembly = value;
                    }
                }
            }

            public override void RegisterEvents()
            {
                PlayerSettlementBehaviour.SettlementCreatedEvent?.AddNonSerializedListener(this, this.SettlementCreated);

                CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnNewGameCreated));
                CampaignEvents.OnGameEarlyLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnGameEarlyLoaded));
                //CampaignEvents.TickEvent.AddNonSerializedListener(this, new Action<float>(this.Tick));
                CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(this.DailyTick));
                CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, new Action<Settlement>(this.DailyTickSettlement));
                CampaignEvents.DailyTickTownEvent.AddNonSerializedListener(this, new Action<Town>(this.DailyTickTown));
                CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, new Action<Settlement, bool, Hero, Hero, Hero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail>(this.SettlementOwnerChanged));
            }

            private void SettlementCreated(Settlement settlement)
            {
                try
                {
                    if (!settlement.IsTown)
                    {
                        return;
                    }

                    var town = settlement.Town;

                    var campaignGameStarter = SandBoxManager.Instance.GameStarter;

                    if (settlement.HasPort)
                    {
                        if (ShipProductionCampaignBehaviorType == null)
                        {
                            if (WarSailsAssembly == null)
                            {
                                WarSailsAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWith("NavalDLC, ", StringComparison.InvariantCultureIgnoreCase));

                            }
                            if (WarSailsAssembly != null)
                            {
                                ShipProductionCampaignBehaviorType = WarSailsAssembly.GetType("NavalDLC.CampaignBehaviors.ShipProductionCampaignBehavior", false, true);

                            }

                        }

                        if (ShipProductionCampaignBehaviorType != null)
                        {
                            var ShipProductionCampaignBehavior = campaignGameStarter.CampaignBehaviors.FirstOrDefault(c => c.GetType() == ShipProductionCampaignBehaviorType);

                            if (DailyTickTownMethod == null)
                            {
                                DailyTickTownMethod = AccessTools.Method(ShipProductionCampaignBehaviorType, "DailyTickTown");

                            }
                            if (DailyTickTownMethod != null && ShipProductionCampaignBehavior != null)
                            {
                                try
                                {
                                    DailyTickTownMethod.Invoke(ShipProductionCampaignBehavior, new object[] { town });
                                }
                                catch (Exception e)
                                {
                                    LogManager.Log.NotifyBad(e);
                                }
                            }
                        }

                        if (town.AvailableShips != null && town.AvailableShips.Count == 0)
                        {
                            ForceAddTownShips(town);
                        }

                        if (town.AvailableShips != null)
                        {
                            for (int i = 0; i < town.AvailableShips.Count; i++)
                            {
                                Ship ship = town.AvailableShips[i];
                                try
                                {
                                    if (ship.Owner == null)
                                    {
                                        ship.Owner = settlement.Party;
                                    }
                                }
                                catch (Exception e)
                                {
                                    LogManager.Log.NotifyBad(e);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    LogManager.Log.NotifyBad(e);
                }

            }

            private void SettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
            {
                DailyTickSettlement(settlement);
            }

            private void DailyTickTown(Town town)
            {
                DailyTickSettlement(town.Settlement);
            }

            private void DailyTickSettlement(Settlement settlement)
            {
                if (settlement != null && settlement.HasPort && settlement.IsTown && settlement.Town?.AvailableShips != null)
                {
                    FixSettlementPortAndShips(settlement);
                }
            }

            private void DailyTick()
            {
                try
                {
                    LogManager.EventTracer.Trace();

                    FixPortsAndShips();
                }
                catch (Exception e)
                {
                    LogManager.Log.NotifyBad(e);
                }
            }

            public override void SyncData(IDataStore dataStore)
            {
                if (!dataStore.IsSaving)
                {
                    OnLoad();
                }
            }
            private void OnNewGameCreated(CampaignGameStarter starter)
            {
                try
                {
                    OnLoad();
                }
                catch (Exception e) { LogManager.Log.NotifyBad(e); }

            }

            /* OnGameEarlyLoaded is only present so that we can still initialize when adding the mod to a save
             * that didn't previously have it enabled (so-called "vanilla save"). This is because SyncData does
             * not even get called during game loading for behaviors that were not previously not part of the save.
             */
            private void OnGameEarlyLoaded(CampaignGameStarter starter)
            {
                try
                {
                    if (!HasLoaded) // if SyncData were to be called, it would've been by now
                    {
                        OnLoad();
                    }
                }
                catch (Exception e) { LogManager.Log.NotifyBad(e); }
            }

            private void OnLoad()
            {
                FixPortsAndShips();

                HasLoaded = true;
            }

            private void FixPortsAndShips()
            {
                // TODO: Load stuff here
                var playerBuiltPorts = CollectPlayerBuiltPorts();
                for (int i = 0; i < playerBuiltPorts.Count; i++)
                {
                    try
                    {
                        var portSettlement = playerBuiltPorts[i];
                        FixSettlementPortAndShips(portSettlement);
                    }
                    catch (Exception e)
                    {
                        LogManager.Log.NotifyBad(e);
                    }
                }
            }

            private static void FixSettlementPortAndShips(Settlement portSettlement)
            {
                try
                {
                    var town = portSettlement.Town;


                    if (town.AvailableShips != null && town.AvailableShips.Count == 0)
                    {
                        ForceAddTownShips(town);
                    }

                    if (town.AvailableShips != null)
                    {
                        for (int j = 0; j < town.AvailableShips.Count; j++)
                        {
                            try
                            {
                                TaleWorlds.CampaignSystem.Naval.Ship ship = town.AvailableShips[j];
                                if (ship.Owner == null)
                                {
                                    ship.Owner = portSettlement.Party;
                                }
                            }
                            catch (Exception e)
                            {
                                LogManager.Log.NotifyBad(e);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    LogManager.Log.NotifyBad(e);
                }
            }

            public static void ForceAddTownShips(Town town, int count = 5)
            {
                if (!Main.IsWarSails)
                {
                    return;
                }

                MBList<ShipHull> mBList = Kingdom.All.SelectMany<Kingdom, ShipHull>((Kingdom x) => x.Culture.AvailableShipHulls).ToMBList<ShipHull>();
                string item = String.Empty;
                int num = count;
                ShipHull randomElement = mBList.GetRandomElement<ShipHull>();
                for (int i = 0; i < num; i++)
                {
                    Ship ship = new Ship(randomElement);
                    town.AvailableShips.Add(ship);
                }
            }
        }
    }
}
