using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Patches;
using BannerlordPlayerSettlement.Saves;
using BannerlordPlayerSettlement.UI;
using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using Helpers;

using SandBox.GauntletUI.Encyclopedia;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Overlay;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;

namespace BannerlordPlayerSettlement.Behaviours
{

    public class PlayerSettlementBehaviour : CampaignBehaviorBase
    {
        public const string PlayerSettlementUnderConstructionMenu = "player_settlement_construction";

        public static PlayerSettlementBehaviour? Instance = null;

        public static bool OldSaveLoaded = false;
        public static bool TriggerSaveAfterUpgrade = false;

        public static readonly string PlayerSettlementTownTemplate = ModulePrefab.LoadModuleFile(Main.ModuleName, "ModuleData", "Templates", "player_settlement_town_template.xml");
        public static readonly string PlayerSettlementCastleTemplate = ModulePrefab.LoadModuleFile(Main.ModuleName, "ModuleData", "Templates", "player_settlement_castle_template.xml");
        public static readonly string PlayerSettlementTownVillageTemplate = ModulePrefab.LoadModuleFile(Main.ModuleName, "ModuleData", "Templates", "player_settlement_town_village_template.xml");
        public static readonly string PlayerSettlementCastleVillageTemplate = ModulePrefab.LoadModuleFile(Main.ModuleName, "ModuleData", "Templates", "player_settlement_castle_village_template.xml");

        public SettlementType SettlementRequest = SettlementType.None;
        public Settlement? RequestBoundSettlement = null;


        private PlayerSettlementInfo _playerSettlementInfo = new();

        private MetaV3? _metaV3 = null;

        public MetaV3? MetaV3 => _metaV3;

        private bool HasLoaded { get; set; }
        public bool ReachedMax
        {
            get
            {
                if (PlayerSettlementInfo.Instance == null || Main.Settings == null)
                {
                    return true;
                }
                return PlayerSettlementInfo.Instance.Towns.Count >= Main.Settings.MaxTowns &&
                    PlayerSettlementInfo.Instance.Towns.All(t => t.Villages.Count >= Main.Settings.MaxVillagesPerTown) &&
                    PlayerSettlementInfo.Instance.Castles.Count >= Main.Settings.MaxCastles &&
                    PlayerSettlementInfo.Instance.Castles.All(t => t.Villages.Count >= Main.Settings.MaxVillagesPerCastle);
            }
        }

        public bool HasRequest
        {
            get
            {
                return SettlementRequest != SettlementType.None;
            }
        }

        public PlayerSettlementBehaviour() : base()
        {
            Instance = this;
        }

        #region Overrides
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnSessionLaunched));

            CampaignEvents.TickEvent.AddNonSerializedListener(this, new Action<float>(this.Tick));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(this.DailyTick));

            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnNewGameCreated));
            CampaignEvents.OnGameEarlyLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnGameEarlyLoaded));
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                if (dataStore.IsSaving)
                {
                    _playerSettlementInfo = PlayerSettlementInfo.Instance ?? new PlayerSettlementInfo();
                    _metaV3 = MetaV3.Create(_playerSettlementInfo);
                }
                dataStore.SyncData("PlayerSettlement_PlayerSettlementInfo", ref _playerSettlementInfo);
                dataStore.SyncData("PlayerSettlement_MetaV3", ref _metaV3);
                _playerSettlementInfo ??= new PlayerSettlementInfo();

                if (!dataStore.IsSaving)
                {
                    OnLoad();
                }
            }
            catch (Exception e)
            {
                Debug.PrintError(e.Message, e.StackTrace);
                Debug.WriteDebugLineOnScreen(e.ToString());
                Debug.SetCrashReportCustomString(e.Message);
                Debug.SetCrashReportCustomStack(e.StackTrace);
            }
        }

        public void LoadEarlySync(IDataStore? dataStore)
        {
            try
            {
                if (dataStore == null)
                {
                    return;
                }

                if (dataStore.IsSaving)
                {
                    return;
                }

                dataStore.SyncData("PlayerSettlement_MetaV3", ref _metaV3);
            }
            catch (Exception e)
            {
                Debug.PrintError(e.Message, e.StackTrace);
                Debug.WriteDebugLineOnScreen(e.ToString());
                Debug.SetCrashReportCustomString(e.Message);
                Debug.SetCrashReportCustomStack(e.StackTrace);
            }
        }
        #endregion

        #region Event Handlers

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            this.SetupGameMenus(starter);
        }

        public void SetupGameMenus(CampaignGameStarter campaignGameSystemStarter)
        {
            try
            {
                campaignGameSystemStarter.AddGameMenu(PlayerSettlementUnderConstructionMenu, "{=!}{SETTLEMENT_INFO}", new OnInitDelegate(PlayerSettlementBehaviour.game_menu_town_under_construction_on_init), GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);
                campaignGameSystemStarter.AddGameMenuOption(PlayerSettlementUnderConstructionMenu, "town_leave", "{=3sRdGQou}Leave", new GameMenuOption.OnConditionDelegate(PlayerSettlementBehaviour.game_menu_town_under_construction_town_leave_on_condition), new GameMenuOption.OnConsequenceDelegate(PlayerSettlementBehaviour.game_menu_town_under_construction_settlement_leave_on_consequence), true, -1, false, null);
            }
            catch (Exception e)
            {
                // Ignore
            }

        }

        [GameMenuInitializationHandler(PlayerSettlementUnderConstructionMenu)]
        public static void game_menu_town_under_construction_menu_enter_sound_on_init(MenuCallbackArgs args)
        {
            args.MenuContext.SetPanelSound("event:/ui/panels/settlement_city");
            args.MenuContext.SetAmbientSound("event:/map/ambient/node/settlements/2d/city");
            args.MenuContext.SetBackgroundMeshName(Settlement.CurrentSettlement.SettlementComponent.WaitMeshName);
        }

        private static void game_menu_town_under_construction_settlement_leave_on_consequence(MenuCallbackArgs args)
        {
            PlayerEncounter.LeaveSettlement();
            PlayerEncounter.Finish(true);
            Campaign.Current.SaveHandler.SignalAutoSave();
        }

        private static bool game_menu_town_under_construction_town_leave_on_condition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            if (MobileParty.MainParty.Army == null)
            {
                return true;
            }
            return MobileParty.MainParty.Army.LeaderParty == MobileParty.MainParty;
        }

        private static void game_menu_town_under_construction_on_init(MenuCallbackArgs args)
        {
            var settlement = Settlement.CurrentSettlement;
            var textObject = ((settlement.OwnerClan != Clan.PlayerClan) ? new TextObject("{=UWzQsHA2}{SETTLEMENT_LINK} is governed by {LORD.LINK}, {FACTION_OFFICIAL} of the {FACTION_TERM}. {PROSPERITY_INFO} {MORALE_INFO}") : new TextObject("{=kXVHwjoV}You have arrived at your fief of {SETTLEMENT_LINK}. {PROSPERITY_INFO} {MORALE_INFO}"));

            settlement.OwnerClan.Leader.SetPropertiesToTextObject(textObject, "LORD");
            string text = settlement.OwnerClan.Leader.MapFaction.Culture.StringId;
            if (settlement.OwnerClan.Leader.IsFemale)
            {
                text += "_f";
            }

            if (settlement.OwnerClan.Leader == Hero.MainHero && !Hero.MainHero.MapFaction.IsKingdomFaction)
            {
                textObject.SetTextVariable("FACTION_TERM", Hero.MainHero.Clan.EncyclopediaLinkWithName);
                textObject.SetTextVariable("FACTION_OFFICIAL", new TextObject("{=hb30yQPN}leader"));
            }
            else
            {
                textObject.SetTextVariable("FACTION_TERM", settlement.MapFaction.EncyclopediaLinkWithName);
                if (settlement.OwnerClan.MapFaction.IsKingdomFaction && settlement.OwnerClan.Leader == settlement.OwnerClan.Leader.MapFaction.Leader)
                {
                    textObject.SetTextVariable("FACTION_OFFICIAL", GameTexts.FindText("str_faction_ruler", text));
                }
                else
                {
                    textObject.SetTextVariable("FACTION_OFFICIAL", GameTexts.FindText("str_faction_official", text));
                }
            }

            textObject.SetTextVariable("SETTLEMENT_LINK", settlement.EncyclopediaLinkWithName);
            settlement.SetPropertiesToTextObject(textObject, "SETTLEMENT_OBJECT");

            textObject.SetTextVariable("PROSPERITY_INFO", "\r\n");
            textObject.SetTextVariable("MORALE_INFO", new TextObject("{=player_settlement_06}This settlement is currently still under construction."));

            MBTextManager.SetTextVariable("SETTLEMENT_INFO", textObject);

            Campaign.Current.GameMenuManager.MenuLocations.Clear();

            Campaign.Current.autoEnterTown = null;

            if (settlement.IsTown)
            {
                args.MenuTitle = new TextObject("{=mVKcvY2U}Town Center", null);
            }
            else if (settlement.IsCastle)
            {
                args.MenuTitle = new TextObject("{=sVXa3zFx}Castle");
            }
            else
            {
                args.MenuTitle = new TextObject("{=Ua6CNLBZ}Village", null);
            }
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            try
            {
                OnLoad();
            }
            catch (Exception e) { Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }

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
            catch (Exception e) { Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
        }

        private void OnLoad()
        {
            _playerSettlementInfo ??= new PlayerSettlementInfo();
            PlayerSettlementInfo.Instance = _playerSettlementInfo;

            PlayerSettlementInfo.Instance.OnLoad();

            HasLoaded = true;
        }

        private void DailyTick()
        {
            if (PlayerSettlementBehaviour.OldSaveLoaded)
            {
                TextObject message = new TextObject("{=player_settlement_08}A player town has been created on a later save. Older saves are not supported and could cause save corruption or town 'ghosting'.", null);
                MBInformationManager.AddQuickInformation(message, 0, null, "");
                InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Colours.Error));
                PlayerSettlementBehaviour.OldSaveLoaded = false;
                return;
            }
            if (PlayerSettlementBehaviour.TriggerSaveAfterUpgrade)
            {
                PlayerSettlementBehaviour.TriggerSaveAfterUpgrade = false;
                SaveHandler.SaveOnly(overwrite: true);
                return;
            }
            if (Main.Settings != null && Main.Settings.Enabled)
            {
                MapBarExtensionVM.Current?.OnRefresh();

                if (PlayerSettlementInfo.Instance != null)
                {
                    var towns = PlayerSettlementInfo.Instance.Towns;
                    if (towns == null)
                    {
                        towns = (PlayerSettlementInfo.Instance.Towns = new List<PlayerSettlementItem>());
                    }

                    for (int t = 0; t < towns.Count; t++)
                    {
                        var town = towns[t];
                        if (!town.BuildComplete && !town.BuildEnd.IsFuture)
                        {
                            NotifyComplete(town);
                        }

                        var villages = town.Villages;
                        if (villages == null)
                        {
                            villages = (town.Villages = new List<PlayerSettlementItem>());
                        }
                        for (int i = 0; i < villages.Count; i++)
                        {
                            var village = villages[i];
                            if (!village.BuildComplete && !village.BuildEnd.IsFuture)
                            {
                                NotifyComplete(village);
                            }
                        }
                    }
                    var castles = PlayerSettlementInfo.Instance.Castles;
                    if (castles == null)
                    {
                        castles = (PlayerSettlementInfo.Instance.Towns = new List<PlayerSettlementItem>());
                    }

                    for (int c = 0; c < castles.Count; c++)
                    {
                        var castle = castles[c];
                        if (!castle.BuildComplete && !castle.BuildEnd.IsFuture)
                        {
                            NotifyComplete(castle);
                        }

                        var villages = castle.Villages;
                        if (villages == null)
                        {
                            villages = (castle.Villages = new List<PlayerSettlementItem>());
                        }
                        for (int i = 0; i < villages.Count; i++)
                        {
                            var village = villages[i];
                            if (!village.BuildComplete && !village.BuildEnd.IsFuture)
                            {
                                NotifyComplete(village);
                            }
                        }
                    }
                }
            }
        }

        public void NotifyComplete(PlayerSettlementItem item)
        {
            item.BuildComplete = true;
            TextObject message = new TextObject("{=player_settlement_07}{TOWN} construction has completed!", null);
            message.SetTextVariable("TOWN", item.SettlementName);
            MBInformationManager.AddQuickInformation(message, 0, null, "");
            InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Colours.Green));

            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
        }

        public void Tick(float delta)
        {
            if (Main.Settings != null && Main.Settings.Enabled && PlayerSettlementInfo.Instance != null)
            {
                if (SettlementRequest == SettlementType.Town)
                {
                    RequestBoundSettlement = null;
                    SettlementRequest = SettlementType.None;

                    BuildTown();
                    return;
                }
                else if (SettlementRequest == SettlementType.Village && RequestBoundSettlement != null)
                {
                    var bound = RequestBoundSettlement;

                    RequestBoundSettlement = null;
                    SettlementRequest = SettlementType.None;

                    BuildVillage(bound);
                    return;
                }
                else if (SettlementRequest == SettlementType.Castle)
                {
                    RequestBoundSettlement = null;
                    SettlementRequest = SettlementType.None;

                    BuildCastle();
                    return;
                }
            }
        }

        private void BuildCastle()
        {
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;

            InformationManager.ShowTextInquiry(new TextInquiryData(new TextObject("{=player_settlement_02}Create Player Settlement").ToString(), new TextObject("{=player_settlement_03}What would you like to name your settlement?").ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                (string settlementName) =>
                {
                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;

                    if (string.IsNullOrEmpty(settlementName))
                    {
                        settlementName = new TextObject("{=player_settlement_n_01}Player Settlement").ToString();
                    }

                    Action<string, CultureObject> apply = (string settlementName, CultureObject culture) =>
                    {
                        if (PlayerSettlementInfo.Instance!.Castles == null)
                        {
                            PlayerSettlementInfo.Instance!.Castles = new();
                        }

                        var castleNumber = PlayerSettlementInfo.Instance!.Castles!.Count + 1;

                        var xml = PlayerSettlementCastleTemplate;
                        xml = xml.Replace("{{POS_X}}", MobileParty.MainParty.Position2D.X.ToString());
                        xml = xml.Replace("{{POS_Y}}", MobileParty.MainParty.Position2D.Y.ToString());
                        /*                         
                          gx = -2.4141
                          gy = -0.9198
                         */
                        xml = xml.Replace("{{G_POS_X}}", (MobileParty.MainParty.Position2D.X - 2.4141f).ToString());
                        xml = xml.Replace("{{G_POS_Y}}", (MobileParty.MainParty.Position2D.Y - 0.9198f).ToString());
                        xml = xml.Replace("{{PLAYER_CULTURE}}", culture.StringId);
                        xml = xml.Replace("{{PLAYER_CLAN}}", Hero.MainHero.Clan.StringId);
                        xml = xml.Replace("{{BASE_IDENTIFIER}}", castleNumber.ToString());
                        xml = xml.Replace("{{SETTLEMENT_NAME}}", settlementName);

                        var castleItem = new PlayerSettlementItem
                        {
                            ItemXML = xml,
                            Identifier = castleNumber,
                            Type = (int) SettlementType.Castle,
                            SettlementName = settlementName,
                        };
                        PlayerSettlementInfo.Instance.Castles.Add(castleItem);

                        var doc = new XmlDocument();
                        doc.LoadXml(xml);
                        MBObjectManager.Instance.LoadXml(doc);

                        var castleStringId = castleItem.GetStringId(SettlementType.Castle);

                        var castleSettlement = MBObjectManager.Instance.GetObject<Settlement>(castleStringId);
                        castleItem.Settlement = castleSettlement;

                        castleSettlement.Town.OwnerClan = Hero.MainHero.Clan;

                        castleSettlement.Name = new TextObject(settlementName);

                        castleSettlement.Party.SetLevelMaskIsDirty();
                        castleSettlement.IsVisible = true;
                        castleSettlement.IsInspected = true;
                        castleSettlement.Town.FoodStocks = (float) castleSettlement.Town.FoodStocksUpperLimit();
                        castleSettlement.Party.SetVisualAsDirty();

                        PartyVisualManager.Current.AddNewPartyVisualForParty(castleSettlement.Party);

                        castleSettlement.OnGameCreated();
                        castleSettlement.OnGameInitialized();
                        castleSettlement.OnFinishLoadState();

                        var castle = castleSettlement.Town;


                        bool flag = false;
                        int num = 0;
                        foreach (BuildingType buildingType1 in BuildingType.All)
                        {
                            if (buildingType1.BuildingLocation != BuildingLocation.Castle || buildingType1 == DefaultBuildingTypes.Wall)
                            {
                                continue;
                            }
                            num = MBRandom.RandomInt(0, 7);
                            if (num < 4)
                            {
                                flag = false;
                                break;
                            }
                            flag = true;
                            num -= 3;
                            if (!flag)
                            {
                                continue;
                            }
                            if (num > 3)
                            {
                                num = 3;
                            }
                            castle.Buildings.Add(new Building(buildingType1, castle, 0f, num));
                        }
                        foreach (BuildingType all2 in BuildingType.All)
                        {
                            if (castle.Buildings.Any<Building>((Building k) => k.BuildingType == all2) || all2.BuildingLocation != BuildingLocation.Castle)
                            {
                                continue;
                            }
                            castle.Buildings.Add(new Building(all2, castle, 0f, 0));
                        }
                        int num1 = MBRandom.RandomInt(1, 4);
                        int num2 = 1;
                        foreach (BuildingType buildingType2 in BuildingType.All)
                        {
                            if (buildingType2.BuildingLocation != BuildingLocation.Daily)
                            {
                                continue;
                            }
                            Building building = new Building(buildingType2, castle, 0f, 1);
                            castle.Buildings.Add(building);
                            if (num2 == num1)
                            {
                                building.IsCurrentlyDefault = true;
                            }
                            num2++;
                        }
                        foreach (Building building1 in
                            from k in castle.Buildings
                            orderby k.CurrentLevel descending
                            select k)
                        {
                            if (building1.CurrentLevel == 3 || building1.CurrentLevel == building1.BuildingType.StartLevel || building1.BuildingType.BuildingLocation == BuildingLocation.Daily)
                            {
                                continue;
                            }
                            castle.BuildingsInProgress.Enqueue(building1);
                        }

                        if (castleSettlement.Town.CurrentDefaultBuilding == null)
                        {
                            BuildingHelper.ChangeDefaultBuilding(castleSettlement.Town.Buildings.FirstOrDefault(), castleSettlement.Town);
                        }

                        //var campaignGameStarter = SandBoxManager.Instance.GameStarter;
                        //var craftingCampaignBehavior = campaignGameStarter.CampaignBehaviors.FirstOrDefault(b => b is CraftingCampaignBehavior) as CraftingCampaignBehavior;
                        //craftingCampaignBehavior?.AddTown(castle, out _);

                        castleItem.BuiltAt = Campaign.CurrentTime;

                        if (Main.Settings!.RequireCastleGold)
                        {
                            GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, castleSettlement, Main.Settings.RequiredCastleGold, true);
                        }

                        SaveHandler.SaveLoad(!Main.Settings.CreateNewSave);
                    };


                    if (Main.Settings!.ForcePlayerCulture)
                    {
                        apply(settlementName, Hero.MainHero.Culture);
                        return;
                    }

                    var titleText = new TextObject("{=player_settlement_20}Choose castle culture");
                    var descriptionText = new TextObject("{=player_settlement_21}Choose the culture for {CASTLE}");
                    descriptionText.SetTextVariable("CASTLE", settlementName);


                    List<InquiryElement> inquiryElements1 = GetCultures(true).Select(c => new InquiryElement(c, c.Name.ToString(), new ImageIdentifier(BannerCode.CreateFrom(c.BannerKey)), true, c.Name.ToString())).ToList();

                    MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                        titleText: titleText.ToString(),
                        descriptionText: descriptionText.ToString(),
                        inquiryElements: inquiryElements1,
                        isExitShown: false,
                        maxSelectableOptionCount: 1,
                        minSelectableOptionCount: 0,
                        affirmativeText: GameTexts.FindText("str_ok", null).ToString(),
                        negativeText: null,
                        affirmativeAction: (List<InquiryElement> args) =>
                        {
                            List<InquiryElement> source = args;
                            InformationManager.HideInquiry();

                            CultureObject culture = (args?.FirstOrDefault()?.Identifier as CultureObject) ?? Hero.MainHero.Culture;

                            apply(settlementName, culture);
                        },
                        negativeAction: null,
                        soundEventPath: "")
                        ,
                        false,
                        false);
                },
                () =>
                {
                    InformationManager.HideInquiry();
                    MapBarExtensionVM.Current?.OnRefresh();
                }, false, new Func<string, Tuple<bool, string>>(CampaignUIHelper.IsStringApplicableForHeroName), "", ""), true, false);
        }

        private void BuildVillage(Settlement bound)
        {
            var villageNumber = PlayerSettlementInfo.Instance!.GetVillageNumber(bound, out PlayerSettlementItem? boundTarget);

            if (villageNumber < 1 || boundTarget == null)
            {
                // Not a valid village
                return;
            }

            if (boundTarget.Villages == null)
            {
                boundTarget.Villages = new();
            }

            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;

            InformationManager.ShowTextInquiry(new TextInquiryData(new TextObject("{=player_settlement_02}Create Player Settlement").ToString(), new TextObject("{=player_settlement_03}What would you like to name your settlement?").ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                (string settlementName) =>
                {
                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;

                    if (string.IsNullOrEmpty(settlementName))
                    {
                        settlementName = new TextObject("{=player_settlement_n_01}Player Settlement").ToString();
                    }

                    Action<string, CultureObject, string> apply = (string settlementName, CultureObject culture, string villageType) =>
                    {
                        var xml = "" + (bound.IsCastle ?
                                            PlayerSettlementCastleVillageTemplate :
                                            PlayerSettlementTownVillageTemplate);
                        xml = xml.Replace("{{POS_X}}", MobileParty.MainParty.Position2D.X.ToString());
                        xml = xml.Replace("{{POS_Y}}", MobileParty.MainParty.Position2D.Y.ToString());
                        xml = xml.Replace("{{PLAYER_CULTURE}}", culture.StringId);
                        xml = xml.Replace("{{BASE_IDENTIFIER}}", boundTarget.Identifier.ToString());
                        xml = xml.Replace("{{SETTLEMENT_NAME}}", settlementName);
                        xml = xml.Replace("{{VILLAGE_NUMBER}}", villageNumber.ToString());
                        xml = xml.Replace("{{VILLAGE_TYPE}}", villageType);

                        var villageItem = new PlayerSettlementItem
                        {
                            ItemXML = xml,
                            Identifier = villageNumber,
                            Type = (int) SettlementType.Village,
                            SettlementName = settlementName,

                        };
                        boundTarget.Villages!.Add(villageItem);

                        var doc = new XmlDocument();
                        doc.LoadXml(xml);
                        MBObjectManager.Instance.LoadXml(doc);

                        var villageStringId = villageItem.GetStringId(SettlementType.Village, boundTarget);

                        var villageSettlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);
                        villageItem.Settlement = villageSettlement;

                        villageSettlement.SetBound(bound);

                        villageSettlement.Name = new TextObject(settlementName);

                        villageSettlement.Party.SetLevelMaskIsDirty();
                        villageSettlement.IsVisible = true;
                        villageSettlement.IsInspected = true;
                        villageSettlement.Party.SetVisualAsDirty();

                        PartyVisualManager.Current.AddNewPartyVisualForParty(villageSettlement.Party);

                        villageSettlement.OnGameCreated();
                        villageSettlement.OnGameInitialized();
                        villageSettlement.OnFinishLoadState();

                        var village = villageSettlement.Village;

                        villageItem.BuiltAt = Campaign.CurrentTime;

                        if (Main.Settings!.RequireVillageGold)
                        {
                            GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, villageSettlement, Main.Settings.RequiredVillageGold, true);
                        }

                        SaveHandler.SaveLoad(!Main.Settings.CreateNewSave);
                    };

                    var determineVillageType = new Action<string, CultureObject>((string settlementName, CultureObject culture) =>
                    {
                        List<InquiryElement> inquiryElements = GetVillageTypeInquiry();

                        var titleText = new TextObject("{=player_settlement_15}Choose village type");
                        var descriptionText = new TextObject("{=player_settlement_16}Choose the type of primary product for {VILLAGE}");
                        descriptionText.SetTextVariable("VILLAGE", settlementName);

                        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                            titleText: titleText.ToString(),
                            descriptionText: descriptionText.ToString(),
                            inquiryElements: inquiryElements,
                            isExitShown: false,
                            maxSelectableOptionCount: 1,
                            minSelectableOptionCount: 0,
                            affirmativeText: GameTexts.FindText("str_ok", null).ToString(),
                            negativeText: null,
                            affirmativeAction: (List<InquiryElement> args) =>
                            {
                                List<InquiryElement> source = args;
                                InformationManager.HideInquiry();

                                string villageType = (args?.FirstOrDefault()?.Identifier as VillageType)?.MeshName ?? AutoCalculateVillageType(villageNumber);

                                apply(settlementName, culture, villageType);
                            },
                            negativeAction: null,
                            soundEventPath: "")
                        ,
                        false,
                        false);
                    });


                    if (Main.Settings!.ForcePlayerCulture)
                    {
                        if (Main.Settings.AutoAllocateVillageType)
                        {
                            apply(settlementName, Hero.MainHero.Culture, AutoCalculateVillageType(villageNumber));
                        }
                        else
                        {
                            determineVillageType(settlementName, Hero.MainHero.Culture);
                        }
                        return;
                    }

                    var titleText = new TextObject("{=player_settlement_11}Choose village culture");
                    var descriptionText = new TextObject("{=player_settlement_12}Choose the culture for {VILLAGE}");
                    descriptionText.SetTextVariable("VILLAGE", settlementName);


                    List<InquiryElement> inquiryElements1 = GetCultures(true).Select(c => new InquiryElement(c, c.Name.ToString(), new ImageIdentifier(BannerCode.CreateFrom(c.BannerKey)), true, c.Name.ToString())).ToList();

                    MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                        titleText: titleText.ToString(),
                        descriptionText: descriptionText.ToString(),
                        inquiryElements: inquiryElements1,
                        isExitShown: false,
                        maxSelectableOptionCount: 1,
                        minSelectableOptionCount: 0,
                        affirmativeText: GameTexts.FindText("str_ok", null).ToString(),
                        negativeText: null,
                        affirmativeAction: (List<InquiryElement> args) =>
                        {
                            List<InquiryElement> source = args;

                            CultureObject culture = (args?.FirstOrDefault()?.Identifier as CultureObject) ?? Hero.MainHero.Culture;

                            if (Main.Settings.AutoAllocateVillageType)
                            {
                                InformationManager.HideInquiry();
                                apply(settlementName, culture, AutoCalculateVillageType(villageNumber));
                            }
                            else
                            {
                                determineVillageType(settlementName, culture);
                            }
                        },
                        negativeAction: null,
                        soundEventPath: "")
                        ,
                        false,
                        false);
                },
                () =>
                {
                    InformationManager.HideInquiry();
                    MapBarExtensionVM.Current?.OnRefresh();
                }, false, new Func<string, Tuple<bool, string>>(CampaignUIHelper.IsStringApplicableForHeroName), "", ""), true, false);
        }

        private void BuildTown()
        {
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;

            InformationManager.ShowTextInquiry(new TextInquiryData(new TextObject("{=player_settlement_02}Create Player Settlement").ToString(), new TextObject("{=player_settlement_03}What would you like to name your settlement?").ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                (string settlementName) =>
                {
                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;

                    if (string.IsNullOrEmpty(settlementName))
                    {
                        settlementName = new TextObject("{=player_settlement_n_01}Player Settlement").ToString();
                    }

                    Action<string, CultureObject> apply = (string settlementName, CultureObject culture) =>
                    {
                        if (PlayerSettlementInfo.Instance!.Towns == null)
                        {
                            PlayerSettlementInfo.Instance!.Towns = new();
                        }

                        var townNumber = PlayerSettlementInfo.Instance!.Towns!.Count + 1;

                        var xml = PlayerSettlementTownTemplate;
                        xml = xml.Replace("{{POS_X}}", MobileParty.MainParty.Position2D.X.ToString());
                        xml = xml.Replace("{{POS_Y}}", MobileParty.MainParty.Position2D.Y.ToString());
                        xml = xml.Replace("{{G_POS_X}}", (MobileParty.MainParty.Position2D.X - 0.8578f).ToString());
                        xml = xml.Replace("{{G_POS_Y}}", (MobileParty.MainParty.Position2D.Y - 4.2689f).ToString());
                        xml = xml.Replace("{{PLAYER_CULTURE}}", culture.StringId);
                        xml = xml.Replace("{{PLAYER_CLAN}}", Hero.MainHero.Clan.StringId);
                        xml = xml.Replace("{{BASE_IDENTIFIER}}", townNumber.ToString());
                        xml = xml.Replace("{{SETTLEMENT_NAME}}", settlementName);

                        var townItem = new PlayerSettlementItem
                        {
                            ItemXML = xml,
                            Identifier = townNumber,
                            Type = (int) SettlementType.Town,
                            SettlementName = settlementName,
                        };
                        PlayerSettlementInfo.Instance.Towns.Add(townItem);

                        var doc = new XmlDocument();
                        doc.LoadXml(xml);
                        MBObjectManager.Instance.LoadXml(doc);

                        var townStringId = townItem.GetStringId(SettlementType.Town);

                        var townSettlement = MBObjectManager.Instance.GetObject<Settlement>(townStringId);
                        townItem.Settlement = townSettlement;

                        townSettlement.Town.OwnerClan = Hero.MainHero.Clan;

                        townSettlement.Name = new TextObject(settlementName);

                        townSettlement.Party.SetLevelMaskIsDirty();
                        townSettlement.IsVisible = true;
                        townSettlement.IsInspected = true;
                        townSettlement.Town.FoodStocks = (float) townSettlement.Town.FoodStocksUpperLimit();
                        townSettlement.Party.SetVisualAsDirty();

                        PartyVisualManager.Current.AddNewPartyVisualForParty(townSettlement.Party);

                        townSettlement.OnGameCreated();
                        townSettlement.OnGameInitialized();
                        townSettlement.OnFinishLoadState();

                        var town = townSettlement.Town;


                        foreach (BuildingType buildingType in BuildingType.All)
                        {
                            if (buildingType.BuildingLocation != BuildingLocation.Settlement || buildingType == DefaultBuildingTypes.Fortifications)
                            {
                                continue;
                            }
                            var num = MBRandom.RandomInt(0, 7);
                            bool flag;
                            if (num < 4)
                            {
                                flag = false;
                            }
                            else
                            {
                                flag = true;
                                num -= 3;

                            }
                            if (!flag)
                            {
                                continue;
                            }
                            if (num > 3)
                            {
                                num = 3;
                            }
                            town.Buildings.Add(new Building(buildingType, town, 0f, num));
                        }
                        foreach (BuildingType all1 in BuildingType.All)
                        {
                            if (town.Buildings.Any<Building>((Building k) => k.BuildingType == all1) || all1.BuildingLocation != BuildingLocation.Settlement)
                            {
                                continue;
                            }
                            town.Buildings.Add(new Building(all1, town, 0f, 0));
                        }
                        int num1 = MBRandom.RandomInt(1, 4);
                        int num2 = 1;
                        foreach (BuildingType buildingType2 in BuildingType.All)
                        {
                            if (buildingType2.BuildingLocation != BuildingLocation.Daily)
                            {
                                continue;
                            }
                            Building building = new Building(buildingType2, town, 0f, 1);
                            town.Buildings.Add(building);
                            if (num2 == num1)
                            {
                                building.IsCurrentlyDefault = true;
                            }
                            num2++;
                        }
                        foreach (Building building1 in
                            from k in town.Buildings
                            orderby k.CurrentLevel descending
                            select k)
                        {
                            if (building1.CurrentLevel == 3 || building1.CurrentLevel == building1.BuildingType.StartLevel || building1.BuildingType.BuildingLocation == BuildingLocation.Daily)
                            {
                                continue;
                            }
                            town.BuildingsInProgress.Enqueue(building1);
                        }

                        if (townSettlement.Town.CurrentDefaultBuilding == null)
                        {
                            BuildingHelper.ChangeDefaultBuilding(townSettlement.Town.Buildings.FirstOrDefault(), townSettlement.Town);
                        }

                        var campaignGameStarter = SandBoxManager.Instance.GameStarter;
                        var craftingCampaignBehavior = campaignGameStarter.CampaignBehaviors.FirstOrDefault(b => b is CraftingCampaignBehavior) as CraftingCampaignBehavior;
                        craftingCampaignBehavior?.AddTown(town, out _);

                        townItem.BuiltAt = Campaign.CurrentTime;

                        if (Main.Settings!.RequireGold)
                        {
                            GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, townSettlement, Main.Settings.RequiredGold, true);
                        }

                        //if (PlayerSettlementInfo.Instance.Towns.Count == 0 || PlayerSettlementInfo.Instance.Towns.Count == 1)
                        //{
                        //    // NB: This is to prevent leaking town details to older saves!
                        //    // Only for first town!
                        //    UpdateUniqueGameId();
                        //}

                        SaveHandler.SaveLoad(!Main.Settings.CreateNewSave);
                    };


                    if (Main.Settings!.ForcePlayerCulture)
                    {
                        apply(settlementName, Hero.MainHero.Culture);
                        return;
                    }

                    var titleText = new TextObject("{=player_settlement_09}Choose town culture");
                    var descriptionText = new TextObject("{=player_settlement_10}Choose the culture for {TOWN}");
                    descriptionText.SetTextVariable("TOWN", settlementName);


                    List<InquiryElement> inquiryElements1 = GetCultures(true).Select(c => new InquiryElement(c, c.Name.ToString(), new ImageIdentifier(BannerCode.CreateFrom(c.BannerKey)), true, c.Name.ToString())).ToList();

                    MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                        titleText: titleText.ToString(),
                        descriptionText: descriptionText.ToString(),
                        inquiryElements: inquiryElements1,
                        isExitShown: false,
                        maxSelectableOptionCount: 1,
                        minSelectableOptionCount: 0,
                        affirmativeText: GameTexts.FindText("str_ok", null).ToString(),
                        negativeText: null,
                        affirmativeAction: (List<InquiryElement> args) =>
                        {
                            List<InquiryElement> source = args;
                            InformationManager.HideInquiry();

                            CultureObject culture = (args?.FirstOrDefault()?.Identifier as CultureObject) ?? Hero.MainHero.Culture;

                            apply(settlementName, culture);
                        },
                        negativeAction: null,
                        soundEventPath: "")
                        ,
                        false,
                        false);
                },
                () =>
                {
                    InformationManager.HideInquiry();
                    MapBarExtensionVM.Current?.OnRefresh();
                }, false, new Func<string, Tuple<bool, string>>(CampaignUIHelper.IsStringApplicableForHeroName), "", ""), true, false);
        }

        private List<InquiryElement> GetVillageTypeInquiry()
        {
            List<InquiryElement> inquiryElements = new();
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.BattanianHorseRanch, DefaultVillageTypes.BattanianHorseRanch.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.BattanianHorseRanch.PrimaryProduction), true, DefaultVillageTypes.BattanianHorseRanch.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.CattleRange, DefaultVillageTypes.CattleRange.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.CattleRange.PrimaryProduction), true, DefaultVillageTypes.CattleRange.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.ClayMine, DefaultVillageTypes.ClayMine.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.ClayMine.PrimaryProduction), true, DefaultVillageTypes.ClayMine.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.DateFarm, DefaultVillageTypes.DateFarm.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.DateFarm.PrimaryProduction), true, DefaultVillageTypes.DateFarm.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.DesertHorseRanch, DefaultVillageTypes.DesertHorseRanch.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.DesertHorseRanch.PrimaryProduction), true, DefaultVillageTypes.DesertHorseRanch.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.EuropeHorseRanch, DefaultVillageTypes.EuropeHorseRanch.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.EuropeHorseRanch.PrimaryProduction), true, DefaultVillageTypes.EuropeHorseRanch.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.Fisherman, DefaultVillageTypes.Fisherman.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.Fisherman.PrimaryProduction), true, DefaultVillageTypes.Fisherman.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.FlaxPlant, DefaultVillageTypes.FlaxPlant.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.FlaxPlant.PrimaryProduction), true, DefaultVillageTypes.FlaxPlant.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.HogFarm, DefaultVillageTypes.HogFarm.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.HogFarm.PrimaryProduction), true, DefaultVillageTypes.HogFarm.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.IronMine, DefaultVillageTypes.IronMine.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.IronMine.PrimaryProduction), true, DefaultVillageTypes.IronMine.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.Lumberjack, DefaultVillageTypes.Lumberjack.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.Lumberjack.PrimaryProduction), true, DefaultVillageTypes.Lumberjack.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.OliveTrees, DefaultVillageTypes.OliveTrees.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.OliveTrees.PrimaryProduction), true, DefaultVillageTypes.OliveTrees.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.SaltMine, DefaultVillageTypes.SaltMine.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.SaltMine.PrimaryProduction), true, DefaultVillageTypes.SaltMine.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.SheepFarm, DefaultVillageTypes.SheepFarm.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.SheepFarm.PrimaryProduction), true, DefaultVillageTypes.SheepFarm.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.SilkPlant, DefaultVillageTypes.SilkPlant.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.SilkPlant.PrimaryProduction), true, DefaultVillageTypes.SilkPlant.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.SilverMine, DefaultVillageTypes.SilverMine.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.SilverMine.PrimaryProduction), true, DefaultVillageTypes.SilverMine.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.SteppeHorseRanch, DefaultVillageTypes.SteppeHorseRanch.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.SteppeHorseRanch.PrimaryProduction), true, DefaultVillageTypes.SteppeHorseRanch.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.SturgianHorseRanch, DefaultVillageTypes.SturgianHorseRanch.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.SturgianHorseRanch.PrimaryProduction), true, DefaultVillageTypes.SturgianHorseRanch.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.VineYard, DefaultVillageTypes.VineYard.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.VineYard.PrimaryProduction), true, DefaultVillageTypes.VineYard.ShortName.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.VlandianHorseRanch, DefaultVillageTypes.VlandianHorseRanch.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.VlandianHorseRanch.PrimaryProduction), true, DefaultVillageTypes.VlandianHorseRanch.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.WheatFarm, DefaultVillageTypes.WheatFarm.ShortName.ToString(), new ImageIdentifier(DefaultVillageTypes.WheatFarm.PrimaryProduction), true, DefaultVillageTypes.WheatFarm.PrimaryProduction.Name.ToString()));

            return inquiryElements;
        }

        private string AutoCalculateVillageType(int villageNumber)
        {
            try
            {
                var el = GetVillageTypeInquiry().GetRandomElement();

                var villageType = el.Identifier as VillageType;

                if (villageType != null)
                {
                    return villageType.MeshName;
                }
            }
            catch (Exception)
            {
            }

            switch (villageNumber)
            {
                default:
                case 1:
                    return "swine_farm";
                case 2:
                    return "lumberjack";
                case 3:
                    return "iron_mine";
            }
        }

        public IEnumerable<CultureObject> GetCultures(bool mainOnly = false)
        {
            foreach (CultureObject objectTypeList in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
            {
                if (mainOnly && !objectTypeList.IsMainCulture)
                {
                    continue;
                }
                yield return objectTypeList;
            }
        }

        static readonly FastInvokeHandler SetUniqueGameId = MethodInvoker.GetHandler(AccessTools.Property(typeof(Campaign), nameof(Campaign.UniqueGameId)).SetMethod);
        public static (string oldId, string newId) UpdateUniqueGameId()
        {
            var oldId = Campaign.Current.UniqueGameId;
            var newId = MiscHelper.GenerateCampaignId(12);
            SetUniqueGameId(Campaign.Current, new object[] { newId });

            return (oldId, Campaign.Current.UniqueGameId);
        }

        public SettlementType GetNextBuildType(out PlayerSettlementItem? boundTarget)
        {
            if (PlayerSettlementInfo.Instance == null || Main.Settings == null)
            {
                boundTarget = null;
                return SettlementType.None;
            }

            if (PlayerSettlementInfo.Instance.Towns == null)
            {
                PlayerSettlementInfo.Instance.Towns = new();
            }

            if (PlayerSettlementInfo.Instance.Towns.Count == 0)
            {
                boundTarget = null;
                return SettlementType.Town;
            }

            var incompleteTown = PlayerSettlementInfo.Instance.Towns.FirstOrDefault(t => t.Villages.Count < Main.Settings.MaxVillagesPerTown);
            if (incompleteTown != null)
            {
                boundTarget = incompleteTown;
                return SettlementType.Village;
            }

            if (PlayerSettlementInfo.Instance.Castles == null)
            {
                PlayerSettlementInfo.Instance.Castles = new();
            }

            var incompleteCastle = PlayerSettlementInfo.Instance.Castles.FirstOrDefault(t => t.Villages.Count < Main.Settings.MaxVillagesPerCastle);
            if (incompleteCastle != null)
            {
                boundTarget = incompleteCastle;
                return SettlementType.Village;
            }

            if (PlayerSettlementInfo.Instance.Towns.Count >= Main.Settings.MaxTowns)
            {
                if (PlayerSettlementInfo.Instance.Castles.Count >= Main.Settings.MaxCastles)
                {
                    boundTarget = null;
                    return SettlementType.None;
                }

                boundTarget = null;
                return SettlementType.Castle;
            }

            if (PlayerSettlementInfo.Instance.Castles.Count >= Main.Settings.MaxCastles)
            {
                boundTarget = null;
                return SettlementType.Town;
            }

            var random = MBRandom.RandomInt(0, 100);
            if (random <= Main.Settings.CastleChance)
            {
                boundTarget = null;
                return SettlementType.Castle;
            }
            else
            {
                boundTarget = null;
                return SettlementType.Town;
            }

        }
        #endregion
    }

    public static class ModulePrefab
    {
        public static XmlDocument LoadResourceAsXML(string embedPath)
        {
            using var stream = typeof(ModulePrefab).Assembly.GetManifestResourceStream(embedPath);
            if (stream is null)
                throw new NullReferenceException($"Could not find embed resource '{embedPath}'!");
            using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreComments = true });
            var doc = new XmlDocument();
            doc.Load(xmlReader);
            return doc;
        }

        public static string LoadModuleFile(string moduleName, params string[] filePaths)
        {
            string fullPath = GetModuleFilePath(moduleName, filePaths);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Unable to find specified file", fullPath);
            }

            return File.ReadAllText(fullPath);
        }

        public static XmlDocument LoadModuleFileAsXML(string moduleName, params string[] filePaths)
        {
            string fullPath = GetModuleFilePath(moduleName, filePaths);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Unable to find specified file", fullPath);
            }

            var doc = new XmlDocument();
            doc.LoadXml(File.ReadAllText(fullPath));
            return doc;
        }

        private static string GetModuleFilePath(string moduleName, string[] filePaths)
        {
            var fileSegments = new List<string>();
            fileSegments.Add(ModuleHelper.GetModuleInfo(moduleName).FolderPath);
            fileSegments.AddRange(filePaths);

            var fullPath = Path.Combine(fileSegments.ToArray());
            return fullPath;
        }
    }
}