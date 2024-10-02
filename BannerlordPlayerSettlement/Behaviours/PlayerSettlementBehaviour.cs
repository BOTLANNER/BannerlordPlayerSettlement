using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Patches;
using BannerlordPlayerSettlement.Saves;
using BannerlordPlayerSettlement.UI;

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

        public static string PlayerSettlementIdentifier => PlayerSettlementInfo.Instance?.PlayerSettlementIdentifier ?? "player_settlement_town_01";

        public static PlayerSettlementBehaviour? Instance = null;

        public static bool OldSaveLoaded = false;
        public static bool TriggerSaveAfterUpgrade = false;

        //internal static string PlayerSettlementTemplate = ResourcePrefab.Load("BannerlordPlayerSettlement.Behaviours.player_settlement_template.xml").OuterXml;
        internal static string PlayerSettlementTemplate = ModulePrefab.LoadModuleFile(Main.ModuleName, "ModuleData", "Templates", "player_settlement_template.xml");

        public bool CreateSettlement = false;

        private PlayerSettlementInfo _playerSettlementInfo = new();
        private bool HasLoaded { get; set; }

        Color Error = new(178 * 255, 34 * 255, 34 * 255);
        Color Warn = new(189 * 255, 38 * 255, 0);
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
                }
                dataStore.SyncData("PlayerSettlement_PlayerSettlementInfo", ref _playerSettlementInfo);
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
            textObject.SetTextVariable("MORALE_INFO", new TextObject("{=player_settlement_06}This town is currently still under construction."));

            MBTextManager.SetTextVariable("SETTLEMENT_INFO", textObject);

            Campaign.Current.GameMenuManager.MenuLocations.Clear();

            Campaign.Current.autoEnterTown = null;
            args.MenuTitle = new TextObject("{=mVKcvY2U}Town Center", null);
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
                InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Error));
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
                if (PlayerSettlementInfo.Instance?.PlayerSettlement == null)
                {
                    // For as long as there is no player settlement, recheck daily whether player is eligible to build settlement.
                    MapBarExtensionVM.Current?.OnRefresh();
                }
                else if (PlayerSettlementInfo.Instance != null && !PlayerSettlementInfo.Instance.BuildComplete && !PlayerSettlementInfo.Instance.BuildEnd.IsFuture)
                {
                    PlayerSettlementInfo.Instance.BuildComplete = true;
                    TextObject message = new TextObject("{=player_settlement_07}{TOWN} construction has completed!", null);
                    message.SetTextVariable("TOWN", PlayerSettlementInfo.Instance.PlayerSettlementName);
                    MBInformationManager.AddQuickInformation(message, 0, null, "");
                }
            }
        }

        public void Tick(float delta)
        {
            if (Main.Settings != null && Main.Settings.Enabled)
            {
                if (CreateSettlement && PlayerSettlementInfo.Instance?.PlayerSettlement == null)
                {
                    CreateSettlement = false;

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
                                string identifierUniqueness = MBRandom.RandomInt().ToString();
                                //MBObjectManager.Instance.LoadOneXmlFromFile(String.Concat(ModuleHelper.GetModuleFullPath("PlayerSettlement"), "ModuleData/player_settlements.xml"), null, true);
                                PlayerSettlementInfo.Instance!.PlayerSettlementXML = PlayerSettlementTemplate;
                                PlayerSettlementInfo.Instance.PlayerSettlementXML = PlayerSettlementInfo.Instance.PlayerSettlementXML.Replace("{{POS_X}}", MobileParty.MainParty.Position2D.X.ToString());
                                PlayerSettlementInfo.Instance.PlayerSettlementXML = PlayerSettlementInfo.Instance.PlayerSettlementXML.Replace("{{POS_Y}}", MobileParty.MainParty.Position2D.Y.ToString());
                                PlayerSettlementInfo.Instance.PlayerSettlementXML = PlayerSettlementInfo.Instance.PlayerSettlementXML.Replace("{{G_POS_X}}", (MobileParty.MainParty.Position2D.X - 0.8578f).ToString());
                                PlayerSettlementInfo.Instance.PlayerSettlementXML = PlayerSettlementInfo.Instance.PlayerSettlementXML.Replace("{{G_POS_Y}}", (MobileParty.MainParty.Position2D.Y - 4.2689f).ToString());
                                PlayerSettlementInfo.Instance.PlayerSettlementXML = PlayerSettlementInfo.Instance.PlayerSettlementXML.Replace("{{PLAYER_CULTURE}}", culture.StringId);
                                PlayerSettlementInfo.Instance.PlayerSettlementXML = PlayerSettlementInfo.Instance.PlayerSettlementXML.Replace("{{PLAYER_CLAN}}", Hero.MainHero.Clan.StringId);
                                PlayerSettlementInfo.Instance.PlayerSettlementXML = PlayerSettlementInfo.Instance.PlayerSettlementXML.Replace("{{TOWN_IDENTIFIER}}", identifierUniqueness);
                                PlayerSettlementInfo.Instance.PlayerSettlementXML = PlayerSettlementInfo.Instance.PlayerSettlementXML.Replace("{{SETTLEMENT_NAME}}", settlementName);

                                PlayerSettlementInfo.Instance.PlayerSettlementIdentifier = "player_settlement_town_{{TOWN_IDENTIFIER}}".Replace("{{TOWN_IDENTIFIER}}", identifierUniqueness);
                                PlayerSettlementInfo.Instance.PlayerSettlementName = settlementName;

                                var doc = new XmlDocument();
                                doc.LoadXml(PlayerSettlementInfo.Instance.PlayerSettlementXML);
                                MBObjectManager.Instance.LoadXml(doc);
                                PlayerSettlementInfo.Instance.PlayerSettlement = MBObjectManager.Instance.GetObject<Settlement>(PlayerSettlementIdentifier);
                                PlayerSettlementInfo.Instance.PlayerSettlement.Town.OwnerClan = Hero.MainHero.Clan;

                                PlayerSettlementInfo.Instance.PlayerSettlement.Name = new TextObject(settlementName);

                                PlayerSettlementInfo.Instance.PlayerSettlement.Party.SetLevelMaskIsDirty();
                                PlayerSettlementInfo.Instance.PlayerSettlement.IsVisible = true;
                                PlayerSettlementInfo.Instance.PlayerSettlement.IsInspected = true;
                                PlayerSettlementInfo.Instance.PlayerSettlement.Town.FoodStocks = (float) PlayerSettlementInfo.Instance.PlayerSettlement.Town.FoodStocksUpperLimit();
                                PlayerSettlementInfo.Instance.PlayerSettlement.Party.SetVisualAsDirty();

                                PartyVisualManager.Current.AddNewPartyVisualForParty(PlayerSettlementInfo.Instance.PlayerSettlement.Party);

                                PlayerSettlementInfo.Instance.PlayerSettlement.OnGameCreated();
                                PlayerSettlementInfo.Instance.PlayerSettlement.OnGameInitialized();
                                PlayerSettlementInfo.Instance.PlayerSettlement.OnFinishLoadState();

                                var town = PlayerSettlementInfo.Instance.PlayerSettlement.Town;


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

                                if (PlayerSettlementInfo.Instance.PlayerSettlement.Town.CurrentDefaultBuilding == null)
                                {
                                    BuildingHelper.ChangeDefaultBuilding(PlayerSettlementInfo.Instance.PlayerSettlement.Town.Buildings.FirstOrDefault(), PlayerSettlementInfo.Instance.PlayerSettlement.Town);
                                }

                                var campaignGameStarter = SandBoxManager.Instance.GameStarter;
                                var craftingCampaignBehavior = campaignGameStarter.CampaignBehaviors.FirstOrDefault(b => b is CraftingCampaignBehavior) as CraftingCampaignBehavior;
                                craftingCampaignBehavior?.AddTown(town, out _);
                                //craftingCampaignBehavior?.CraftingOrders?.AddItem(new KeyValuePair<Town, CraftingCampaignBehavior.CraftingOrderSlots>(town, new CraftingCampaignBehavior.CraftingOrderSlots()));

                                _playerSettlementInfo.BuiltAt = Campaign.CurrentTime;

                                if (Main.Settings.RequireGold)
                                {
                                    GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, PlayerSettlementInfo.Instance.PlayerSettlement, Main.Settings.RequiredGold, true);
                                }

                                // NB: This is to prevent leaking town details to older saves!
                                UpdateUniqueGameId();

                                SaveHandler.SaveLoad((saveName) =>
                                {
                                    var userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mount and Blade II Bannerlord");
                                    var moduleName = Main.Name;

                                    //var Module = $"{moduleName}.{GetType().Name}";
                                    var ConfigDir = Path.Combine(userDir, "Configs", moduleName, Campaign.Current.UniqueGameId);

                                    if (!Directory.Exists(ConfigDir))
                                    {
                                        Directory.CreateDirectory(ConfigDir);
                                    }

                                    var configFile = Path.Combine(ConfigDir, $"PlayerSettlement.xml");
                                    File.WriteAllText(configFile, PlayerSettlementInfo.Instance.PlayerSettlementXML);

                                    var metaFile = Path.Combine(ConfigDir, $"meta.bin");
                                    var metaText = "";
                                    metaText += PlayerSettlementInfo.Instance.PlayerSettlementIdentifier.Base64Encode();
                                    metaText += "\r\n";
                                    metaText += PlayerSettlementInfo.Instance.PlayerSettlementName.Base64Encode();
                                    metaText += "\r\n";
                                    metaText += PlayerSettlementInfo.Instance.BuiltAt.ToString().Base64Encode();
                                    metaText += "\r\n";
                                    metaText += Main.Version.Base64Encode();
                                    File.WriteAllText(metaFile, metaText);
                                });
                            };


                            if (Main.Settings.ForcePlayerCulture)
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