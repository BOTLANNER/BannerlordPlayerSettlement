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
using TaleWorlds.CampaignSystem.Settlements.Buildings;
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

        private Type? NavalBuildingTypesType;
        private Type? NavalDLCExtensionsType;

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

                NavalBuildingTypesType = assembly.GetType("NavalDLC.Settlements.Building.NavalBuildingTypes", false, true);
                if (NavalBuildingTypesType != null)
                {
                    NavalDLCExtensions.SettlementShipyardProp = AccessTools.Property(NavalBuildingTypesType, nameof(NavalDLCExtensions.SettlementShipyard));
                }

                NavalDLCExtensionsType = assembly.GetType("NavalDLC.NavalDLCExtensions", false, true);
                if (NavalDLCExtensionsType != null)
                {
                    NavalDLCExtensions.GetShipyardMethod = AccessTools.Method(NavalDLCExtensionsType, nameof(NavalDLCExtensions.GetShipyard));
                    NavalDLCExtensions.GetAvailableShipUpgradePiecesMethod = AccessTools.Method(NavalDLCExtensionsType, nameof(NavalDLCExtensions.GetAvailableShipUpgradePieces));
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
                PlayerSettlementBehaviour.SettlementRebuildEvent?.AddNonSerializedListener(this, this.SettlementRebuilt);
                PlayerSettlementBehaviour.SettlementOverwriteEvent?.AddNonSerializedListener(this, this.SettlementOverwritten);

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
                CheckSettlementPort(settlement, true);
            }

            private void SettlementRebuilt(Settlement settlement)
            {
                CheckSettlementPort(settlement, false);
                CheckShipyard(settlement);
            }

            private void SettlementOverwritten(Settlement settlement)
            {
                CheckSettlementPort(settlement, false);
            }

            private void CheckSettlementPort(Settlement settlement, bool forceShips)
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

                        if (town.AvailableShips != null && town.AvailableShips.Count == 0 && forceShips)
                        {
                            // Even though the ShipTradeCampaignBehavior would over time fill available ships, it should be prepopulated at first build.
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

                    CheckShipyard(settlement);
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

                        CheckShipyard(portSettlement);
                    }
                    catch (Exception e)
                    {
                        LogManager.Log.NotifyBad(e);
                    }
                }
            }

            private static void CheckShipyard(Settlement portSettlement)
            {
                if (!portSettlement.HasPort || !portSettlement.IsTown)
                {
                    return;
                }

                // This extension adds a shipyard if not found
                var shipyard = portSettlement.Town.GetShipyard();
                if (shipyard == null)
                {
                    LogManager.Log.NotifyBad($"{portSettlement} has a port but not a shipyard!");
                }

            }

            private static void FixSettlementPortAndShips(Settlement portSettlement)
            {
                try
                {
                    var town = portSettlement.Town;

                    // Don't forcibly add available ships here. The ShipTradeCampaignBehavior will over time fill it.
                    //if (town.AvailableShips != null && town.AvailableShips.Count == 0)
                    //{
                    //    ForceAddTownShips(town);
                    //}

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

                try
                {
                    MBList<ShipHull> mBList = Kingdom.All.SelectMany<Kingdom, ShipHull>((Kingdom x) => x.Culture.AvailableShipHulls).ToMBList<ShipHull>();
                    string item = String.Empty;
                    int num = count;
                    //ShipHull randomElement = mBList.GetRandomElement<ShipHull>();
                    for (int i = 0; i < num; i++)
                    {
                        try
                        {
                            ShipHull randomShipHull = GetRandomShipHull(town);
                            if (randomShipHull == null)
                            {
                                continue;
                            }
                            Ship ship = new Ship(randomShipHull);
                            List<ShipUpgradePiece> availableShipUpgradePieces = town.GetAvailableShipUpgradePieces();
                            availableShipUpgradePieces.Shuffle();
                            foreach (KeyValuePair<string, ShipSlot> availableSlot in ship.ShipHull.AvailableSlots)
                            {
                                if (!(MBRandom.RandomFloat > 0.5f))
                                {
                                    continue;
                                }

                                int numSub = MBRandom.RandomInt(availableShipUpgradePieces.Count);
                                for (int iSub = 0; iSub < availableShipUpgradePieces.Count; iSub++)
                                {
                                    ShipUpgradePiece shipUpgradePiece = availableShipUpgradePieces[(iSub + numSub) % availableShipUpgradePieces.Count];
                                    if (shipUpgradePiece.DoesPieceMatchSlot(availableSlot.Value))
                                    {
                                        ship.SetPieceAtSlot(availableSlot.Key, shipUpgradePiece);
                                        break;
                                    }
                                }
                            }

                            ChangeShipOwnerAction.ApplyByProduction(town.Settlement.Party, ship);
                            CampaignEventDispatcher.Instance.OnShipCreated(ship, town.Settlement);
                        }
                        catch (Exception e)
                        {
                            LogManager.Log.NotifyBad(e);
                        }
                    }

                    ExplainedNumber result = default(ExplainedNumber);
                    town.AddEffectOfBuildings(BuildingEffectEnum.MaximumShipCount, ref result);

                    int idealShipCountForTown = (int) result.ResultNumber;
                    if (town.AvailableShips.Count >= idealShipCountForTown)
                    {
                        TryRemoveExcessShipsFromTown(town, idealShipCountForTown);
                    }
                }
                catch (Exception e)
                {
                    // Prefer defensive coding to avoid crashes. Ignoring failures are fine here as it just means less available ships.
                    LogManager.Log.NotifyBad(e);
                }
            }

            private static void TryRemoveExcessShipsFromTown(Town town, int idealShipCountForTown)
            {
                int num = town.AvailableShips.Count - idealShipCountForTown;
                if (num <= 0)
                {
                    return;
                }

                List<Ship> shipsOfOtherCulture = town.AvailableShips.Where((Ship x) => !town.Culture.AvailableShipHulls.Contains(x.ShipHull)).ToList();
                foreach (Ship item in shipsOfOtherCulture)
                {
                    if (MBRandom.RandomFloat < 0.7f)
                    {
                        DestroyShipAction.Apply(item);
                        num--;
                        if (num < 0)
                        {
                            break;
                        }
                    }
                }

                if (num <= 0)
                {
                    return;
                }

                foreach (Ship item2 in town.AvailableShips.Where((Ship x) => !shipsOfOtherCulture.Contains(x)).ToList())
                {
                    if (MBRandom.RandomFloat < 0.3f)
                    {
                        DestroyShipAction.Apply(item2);
                        num--;
                        if (num < 0)
                        {
                            break;
                        }
                    }
                }
            }

            private static ShipHull GetRandomShipHull(Town town)
            {
                MBList<(ShipHull, float)> availableShipHullsForTown = GetAvailableShipHullsForTown(town);
                if (availableShipHullsForTown.Count == 0)
                {
                    Debug.FailedAssert("Could not find ships to create.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\NavalDLC\\CampaignBehaviors\\ShipProductionCampaignBehavior.cs", "GetRandomShipHull", 231);
                }

                return MBRandom.ChooseWeighted(availableShipHullsForTown);
            }

            private static MBList<(ShipHull, float)> GetAvailableShipHullsForTown(Town town)
            {
                MBList<(ShipHull, float)> mBList = new MBList<(ShipHull, float)>();
                foreach (ShipHull availableShipHull in town.Culture.AvailableShipHulls)
                {
                    if (CanTownCreateShipFromHull(town, availableShipHull))
                    {
                        mBList.Add((availableShipHull, availableShipHull.ProductionBuildWeight));
                    }
                }

                return mBList;
            }

            private static bool CanTownCreateShipFromHull(Town town, ShipHull shipHull)
            {
                return shipHull.Type switch
                {
                    ShipHull.ShipType.Light => town.GetShipyard().CurrentLevel > 0,
                    ShipHull.ShipType.Medium => town.GetShipyard().CurrentLevel > 1,
                    ShipHull.ShipType.Heavy => town.GetShipyard().CurrentLevel == 3,
                    _ => false,
                };
            }
        }

    }
    static class NavalDLCExtensions
    {
        internal static MethodInfo GetShipyardMethod = null;
        internal static MethodInfo GetAvailableShipUpgradePiecesMethod = null;

        internal static PropertyInfo SettlementShipyardProp = null;


        internal static BuildingType SettlementShipyard => SettlementShipyardProp != null ? SettlementShipyardProp.GetValue(null) as BuildingType : null;

        public static Building GetShipyard(this Town town)
        {
            Building shipyard = null;
            try
            {
                if (GetShipyardMethod != null)
                {
                    shipyard = GetShipyardMethod.Invoke(null, new object[] { town }) as Building;
                }

                if (shipyard == null)
                {
                    shipyard = town.Buildings.FirstOrDefault(b => b.BuildingType == SettlementShipyard);
                }

                if (shipyard == null && SettlementShipyard != null)
                {
                    shipyard = new Building(SettlementShipyard, town, 0f, SettlementShipyard.StartLevel);
                    town.Buildings.Add(shipyard);
                }
            }
            catch (Exception e)
            {
                LogManager.Log.NotifyBad(e);
            }
            return shipyard;
        }


        public static List<ShipUpgradePiece> GetAvailableShipUpgradePieces(this Town town)
        {
            if (GetAvailableShipUpgradePiecesMethod != null)
            {
                return GetAvailableShipUpgradePiecesMethod.Invoke(null, new object[] { town }) as List<ShipUpgradePiece>;
            }
            return new();
        }
    }
}
