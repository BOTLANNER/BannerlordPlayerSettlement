using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using BannerlordPlayerSettlement.Descriptors;
using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Patches;
using BannerlordPlayerSettlement.Saves;
using BannerlordPlayerSettlement.UI.Viewmodels;
using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using Helpers;

using SandBox;
using SandBox.View.Map;
using SandBox.View.Map.Managers;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;

using GameOverlays = TaleWorlds.CampaignSystem.GameMenus.GameMenu;

namespace BannerlordPlayerSettlement.Behaviours
{

    public class PlayerSettlementBehaviour : CampaignBehaviorBase
    {
        static FastInvokeHandler FillGarrisonPartyOnNewGameInvoker = MethodInvoker.GetHandler(AccessTools.Method(typeof(GarrisonTroopsCampaignBehavior), "FillGarrisonPartyOnNewGame"));

        public const string PlayerSettlementUnderConstructionMenu = "player_settlement_construction";

        public static PlayerSettlementBehaviour? Instance = null;

        public static bool OldSaveLoaded = false;
        public static bool TriggerSaveAfterUpgrade = false;
        public static bool TriggerSaveLoadAfterUpgrade = false;

        public SettlementType SettlementRequest = SettlementType.None;
        public PlayerSettlementItem? ReSettlementRequest = null;
        public Settlement? OverwriteRequest = null;


        private PlayerSettlementInfo _playerSettlementInfo = new();

        private MetaV3? _metaV3 = null;

        public MetaV3? MetaV3 => _metaV3;

        private readonly MbEvent<Settlement> _settlementCreated = new MbEvent<Settlement>();

        public static IMbEvent<Settlement>? SettlementCreatedEvent
        {
            get
            {
                return PlayerSettlementBehaviour.Instance?._settlementCreated;
            }
        }

        private readonly MbEvent<Settlement> _settlementBuildComplete = new MbEvent<Settlement>();

        public static IMbEvent<Settlement>? SettlementBuildCompleteEvent
        {
            get
            {
                return PlayerSettlementBehaviour.Instance?._settlementBuildComplete;
            }
        }

        private readonly MbEvent _onReset = new MbEvent();

        public static IMbEvent? OnResetEvent
        {
            get
            {
                return PlayerSettlementBehaviour.Instance?._onReset;
            }
        }


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
                       PlayerSettlementInfo.Instance.Castles.Count >= Main.Settings.MaxCastles &&
                       PlayerSettlementInfo.Instance.TotalVillages >= Settings.HardMaxVillages;
            }
        }

        public bool HasRequest
        {
            get
            {
                return SettlementRequest != SettlementType.None || ReSettlementRequest != null || OverwriteRequest != null;
            }
        }

        public PlayerSettlementBehaviour() : base()
        {
            Instance = this;
        }

        #region Settlement Placement
        const string GhostSettlementEntityId = "player_settlement_ghost";

        private List<PlayerSettlementItemTemplate> availableModels = new List<PlayerSettlementItemTemplate>();
        private int currentModelOptionIdx = -1;
        private GameEntity? settlementVisualEntity = null;
        private MatrixFrame? settlementPlacementFrame = null;
        private Action? applyPending = null;
        private float holdTime = 0f;
        private string? settlementVisualPrefab = null;
        #endregion

        #region Settlement Deep Edit
        private bool deepEdit = false;
        private GameEntity? currentDeepTarget;
        private float deepEditScale = 1f;
        private string? deepEditPrefab = null;

        private List<DeepTransformEdit> deepTransformEdits = new List<DeepTransformEdit>();
        private List<GameEntity> settlementVisualEntityChildren = new();
        #endregion

        #region Gate Placement
        const string GhostGateEntityId = "player_settlement_ghost_gate";
        const string GhostGatePrefabId = GhostGateEntityId;
        private GameEntity? ghostGateVisualEntity = null;
        private MatrixFrame? gatePlacementFrame = null;

        bool gateSupported = false;
        #endregion

        public bool IsPlacingSettlement => settlementVisualEntity != null && applyPending != null;
        public bool IsDeepEdit => deepEdit && currentDeepTarget != null && IsPlacingSettlement && !IsPlacingGate;
        public bool IsPlacingGate => ghostGateVisualEntity != null && applyPending != null;

        #region Overrides
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnSessionLaunched));

            CampaignEvents.TickEvent.AddNonSerializedListener(this, new Action<float>(this.Tick));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(this.DailyTick));

            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnNewGameCreated));
            CampaignEvents.OnGameEarlyLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnGameEarlyLoaded));

            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, SettlementEntered);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, SettlementLeft);
        }

        private void SettlementLeft(MobileParty arg1, Settlement arg2)
        {
            if (arg1 == MobileParty.MainParty)
            {
                MapBarExtensionVM.Current?.Tick(0f);
            }
        }

        private void SettlementEntered(MobileParty arg1, Settlement arg2, Hero arg3)
        {
            if (arg1 == MobileParty.MainParty || arg3 == Hero.MainHero)
            {
                MapBarExtensionVM.Current?.Tick(0f);
            }
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
                LogManager.Log.NotifyBad(e);
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
                LogManager.Log.NotifyBad(e);
            }
        }
        #endregion

        #region Event Handlers

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            this.SetupGameMenus(starter);

            this.SetupConversationDialogues(starter);
        }

        private void SetupConversationDialogues(CampaignGameStarter starter)
        {
            starter.AddPlayerLine("player_settlement_build_options_intro", "hero_main_options", "player_settlement_build_options_response", "{=player_settlement_25}I would like to review my options for building a settlement.", new ConversationSentence.OnConditionDelegate(this.conv_build_start_condition), null, 100, new ConversationSentence.OnClickableConditionDelegate(this.conv_build_start_clickable), null);
            starter.AddDialogLine("player_settlement_build_options_response_dialogue", "player_settlement_build_options_response", "player_settlement_build_options_choices", "{=k7ebznzr}Yes?", null, null, 100, null);
            starter.AddPlayerLine("player_settlement_rebuild_settlement", "player_settlement_build_options_choices", "close_window", "{=player_settlement_40}We should rebuild this settlement.", new ConversationSentence.OnConditionDelegate(() => conv_rebuild_condition()), new ConversationSentence.OnConsequenceDelegate(() => conv_rebuild_consequence()), 100, new ConversationSentence.OnClickableConditionDelegate((out TextObject t) => conv_rebuild_clickable(out t)), null);
            starter.AddPlayerLine("player_settlement_build_town", "player_settlement_build_options_choices", "close_window", "{=player_settlement_26}We should build a town.", new ConversationSentence.OnConditionDelegate(this.conv_build_town_condition), new ConversationSentence.OnConsequenceDelegate(() => this.conv_build_consequence(SettlementType.Town)), 100, new ConversationSentence.OnClickableConditionDelegate(this.conv_build_town_clickable), null);
            starter.AddPlayerLine("player_settlement_build_village", "player_settlement_build_options_choices", "close_window", "{=player_settlement_27}We should build a village.", new ConversationSentence.OnConditionDelegate(this.conv_build_village_condition), new ConversationSentence.OnConsequenceDelegate(() => this.conv_build_consequence(SettlementType.Village)), 100, new ConversationSentence.OnClickableConditionDelegate(this.conv_build_village_clickable), null);
            starter.AddPlayerLine("player_settlement_build_castle", "player_settlement_build_options_choices", "close_window", "{=player_settlement_28}We should build a castle.", new ConversationSentence.OnConditionDelegate(this.conv_build_castle_condition), new ConversationSentence.OnConsequenceDelegate(() => this.conv_build_consequence(SettlementType.Castle)), 100, new ConversationSentence.OnClickableConditionDelegate(this.conv_build_castle_clickable), null);
            starter.AddPlayerLine("player_settlement_build_nothing", "player_settlement_build_options_choices", "close_window", "{=player_settlement_29}Nevermind.", null, null, 100, null, null);
        }

        private static void conv_rebuild_consequence()
        {
            PlayerSettlementItem? item = null;
            if (Settlement.CurrentSettlement == null || (Settlement.CurrentSettlement.Owner != Hero.MainHero) || Instance == null)
            {
                return;
            }

            Settlement.CurrentSettlement.IsPlayerBuilt(out item);

            Instance.ReSettlementRequest = item;

            if (item == null)
            {
                // If not a player built item, treat as overwrite
                Instance.OverwriteRequest = Settlement.CurrentSettlement;
            }

            MapBarExtensionVM.Current?.OnRefresh();

            Mission.Current?.EndMission();

            if (PlayerEncounter.Current != null)
            {
                PlayerEncounter.LeaveEncounter = true;
            }
        }

        private static bool conv_rebuild_clickable(out TextObject explanation, bool noConversation = false)
        {
            explanation = new TextObject("");

            if (!noConversation)
            {
                if (Hero.OneToOneConversationHero == null || Hero.OneToOneConversationHero.Clan != Clan.PlayerClan)
                {
                    return false;
                }
            }

            if (Main.Settings == null || PlayerSettlementInfo.Instance == null || PlayerSettlementBehaviour.Instance == null)
            {
                return false;
            }

            if (!noConversation && Main.Settings.NoDialogue)
            {
                return false;
            }

            PlayerSettlementItem? item = null;
            if (Settlement.CurrentSettlement == null || (Settlement.CurrentSettlement.Owner != Hero.MainHero))
            {
                return false;
            }

            Settlement.CurrentSettlement.IsPlayerBuilt(out item);

            if (!Settlement.CurrentSettlement.IsVillage && !noConversation && Settlement.CurrentSettlement?.Town?.Governor != Hero.OneToOneConversationHero)
            {
                return false;
            }

            bool enabled = false;
            var type = item?.GetSettlementType() ?? Settlement.CurrentSettlement?.GetSettlementType() ?? SettlementType.None;
            switch (type)
            {
                default:
                case SettlementType.None:
                    enabled = false;
                    break;
                case SettlementType.Town:
                    {
                        enabled = !Main.Settings.RequireGold || (Hero.MainHero.Clan.Gold >= Main.Settings.RebuildTownRequiredGold);
                        explanation = enabled ? explanation : new TextObject("{=player_settlement_h_05}Not enough funds ({CURRENT_FUNDS}/{REQUIRED_FUNDS})");
                        explanation.SetTextVariable("CURRENT_FUNDS", Hero.MainHero?.Gold ?? 0);
                        explanation.SetTextVariable("REQUIRED_FUNDS", Main.Settings.RebuildTownRequiredGold);
                    }
                    break;
                case SettlementType.Village:
                    {
                        enabled = !Main.Settings.RequireVillageGold || (Hero.MainHero.Clan.Gold >= Main.Settings.RebuildVillageRequiredGold);
                        explanation = enabled ? explanation : new TextObject("{=player_settlement_h_05}Not enough funds ({CURRENT_FUNDS}/{REQUIRED_FUNDS})");
                        explanation.SetTextVariable("CURRENT_FUNDS", Hero.MainHero?.Gold ?? 0);
                        explanation.SetTextVariable("REQUIRED_FUNDS", Main.Settings.RebuildVillageRequiredGold);
                    }
                    break;
                case SettlementType.Castle:
                    {
                        enabled = !Main.Settings.RequireCastleGold || (Hero.MainHero.Clan.Gold >= Main.Settings.RebuildCastleRequiredGold);
                        explanation = enabled ? explanation : new TextObject("{=player_settlement_h_05}Not enough funds ({CURRENT_FUNDS}/{REQUIRED_FUNDS})");
                        explanation.SetTextVariable("CURRENT_FUNDS", Hero.MainHero?.Gold ?? 0);
                        explanation.SetTextVariable("REQUIRED_FUNDS", Main.Settings.RebuildCastleRequiredGold);
                    }
                    break;
            }
            return enabled;
        }

        private static bool conv_rebuild_condition(bool noConversation = false)
        {
            if (!noConversation)
            {
                if (Hero.OneToOneConversationHero == null || Hero.OneToOneConversationHero.Clan != Clan.PlayerClan)
                {
                    return false;
                }
            }

            if (Main.Settings == null || PlayerSettlementInfo.Instance == null || PlayerSettlementBehaviour.Instance == null || PlayerSettlementBehaviour.Instance.HasRequest)
            {
                return false;
            }

            if (!noConversation && Main.Settings.NoDialogue)
            {
                return false;
            }

            PlayerSettlementItem? item = null;
            if (Settlement.CurrentSettlement == null || (Settlement.CurrentSettlement.Owner != Hero.MainHero))
            {
                return false;
            }

            Settlement.CurrentSettlement.IsPlayerBuilt(out item);

            if (!Settlement.CurrentSettlement.IsVillage && !noConversation && Settlement.CurrentSettlement?.Town?.Governor != Hero.OneToOneConversationHero)
            {
                return false;
            }

            if ((Main.Settings.Enabled && !Main.Settings.HideButtonUntilReady))
            {
                return true;
            }

            var type = item?.GetSettlementType() ?? Settlement.CurrentSettlement?.GetSettlementType() ?? SettlementType.None;

            return type switch
            {
                SettlementType.Town => !Main.Settings.RequireGold || (Hero.MainHero.Clan.Gold >= Main.Settings.RebuildTownRequiredGold),
                SettlementType.Village => !Main.Settings.RequireVillageGold || (Hero.MainHero.Clan.Gold >= Main.Settings.RebuildVillageRequiredGold),
                SettlementType.Castle => !Main.Settings.RequireCastleGold || (Hero.MainHero.Clan.Gold >= Main.Settings.RebuildCastleRequiredGold),
                _ => false,
            };
        }

        private bool conv_build_castle_clickable(out TextObject? explanation)
        {
            MapBarExtensionVM.Current?.OnRefresh();
            explanation = MapBarExtensionVM.Current?.PlayerSettlementInfo?.PlayerCastleBuildInfo?.DisableHint?.HintText ?? null;
            return (MapBarExtensionVM.Current?.PlayerSettlementInfo?.PlayerCastleBuildInfo?.IsCreatePlayerSettlementAllowed ?? false);
        }

        private bool conv_build_castle_condition()
        {
            MapBarExtensionVM.Current?.OnRefresh();
            return (Main.Settings != null && !Main.Settings.HideButtonUntilReady) || (MapBarExtensionVM.Current?.PlayerSettlementInfo?.PlayerCastleBuildInfo?.IsCreatePlayerSettlementAllowed ?? false);
        }

        private bool conv_build_village_clickable(out TextObject? explanation)
        {
            MapBarExtensionVM.Current?.OnRefresh();
            explanation = MapBarExtensionVM.Current?.PlayerSettlementInfo?.PlayerVillageBuildInfo?.DisableHint?.HintText ?? null;
            return (MapBarExtensionVM.Current?.PlayerSettlementInfo?.PlayerVillageBuildInfo?.IsCreatePlayerSettlementAllowed ?? false);
        }

        private bool conv_build_village_condition()
        {
            MapBarExtensionVM.Current?.OnRefresh();
            return (Main.Settings != null && !Main.Settings.HideButtonUntilReady) || (MapBarExtensionVM.Current?.PlayerSettlementInfo?.PlayerVillageBuildInfo?.IsCreatePlayerSettlementAllowed ?? false);
        }

        private bool conv_build_town_clickable(out TextObject? explanation)
        {
            MapBarExtensionVM.Current?.OnRefresh();
            explanation = MapBarExtensionVM.Current?.PlayerSettlementInfo?.PlayerTownBuildInfo?.DisableHint?.HintText ?? null;
            return (MapBarExtensionVM.Current?.PlayerSettlementInfo?.PlayerTownBuildInfo?.IsCreatePlayerSettlementAllowed ?? false);
        }

        private bool conv_build_town_condition()
        {
            MapBarExtensionVM.Current?.OnRefresh();
            return (Main.Settings != null && !Main.Settings.HideButtonUntilReady) || (MapBarExtensionVM.Current?.PlayerSettlementInfo?.PlayerTownBuildInfo?.IsCreatePlayerSettlementAllowed ?? false);
        }

        private bool conv_build_start_clickable(out TextObject? explanation)
        {
            MapBarExtensionVM.Current?.OnRefresh();
            explanation = MapBarExtensionVM.Current?.PlayerSettlementInfo?.DisableHint?.HintText ?? null;
            bool canRebuild = conv_rebuild_clickable(out TextObject explanation2);
            if (!canRebuild && explanation2 != null && !string.IsNullOrEmpty(explanation2.ToString()))
            {
                var rebuildReason = new TextObject("{=player_settlement_h_13}Cannot rebuild settlement: {REASON}");
                rebuildReason.SetTextVariable("REASON", explanation2);
                if (explanation == null)
                {
                    explanation = rebuildReason;
                }
                else
                {
                    explanation = new TextObject($"{explanation.ToString()}\r\n\r\n{rebuildReason.ToString()}");
                }
            }
            bool allowed = canRebuild || (MapBarExtensionVM.Current?.PlayerSettlementInfo?.IsOverallAllowed ?? false);
            //if (allowed)
            //{
            //    // Clear out tooltip hint because at least one option is available
            //    explanation = null;
            //}
            return allowed;
        }

        private bool conv_build_start_condition()
        {
            if (Hero.OneToOneConversationHero == null || Hero.OneToOneConversationHero.Clan != Clan.PlayerClan)
            {
                return false;
            }

            if (Main.Settings == null || Main.Settings.NoDialogue)
            {
                return false;
            }

            MapBarExtensionVM.Current?.OnRefresh();

            bool canRebuild = conv_rebuild_condition();

            return Main.Settings != null && Main.Settings.Enabled &&
                !this.HasRequest &&
                 (!Main.Settings.HideButtonUntilReady ||
                  (canRebuild) ||
                  (MapBarExtensionVM.Current?.PlayerSettlementInfo?.IsOverallAllowed ?? false));
        }

        private void conv_build_consequence(SettlementType settlementType)
        {
            SettlementRequest = settlementType;

            MapBarExtensionVM.Current?.OnRefresh();

            Campaign.Current.TimeControlMode = CampaignTimeControlMode.UnstoppablePlay;

            Mission.Current?.EndMission();

            if (PlayerEncounter.Current != null)
            {
                PlayerEncounter.LeaveEncounter = true;
            }
        }

        public void SetupGameMenus(CampaignGameStarter campaignGameSystemStarter)
        {
            try
            {
                campaignGameSystemStarter.AddGameMenu(PlayerSettlementUnderConstructionMenu, "{=!}{SETTLEMENT_INFO}", new OnInitDelegate(PlayerSettlementBehaviour.game_menu_town_under_construction_on_init), GameMenu.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);
                campaignGameSystemStarter.AddGameMenuOption(PlayerSettlementUnderConstructionMenu, "town_leave", "{=3sRdGQou}Leave", new GameMenuOption.OnConditionDelegate(PlayerSettlementBehaviour.game_menu_town_under_construction_town_leave_on_condition), new GameMenuOption.OnConsequenceDelegate(PlayerSettlementBehaviour.game_menu_town_under_construction_settlement_leave_on_consequence), true, -1, false, null);

                campaignGameSystemStarter.AddGameMenuOption("town", "leave_rebuild", "{=player_settlement_41}Rebuild Settlement", new GameMenuOption.OnConditionDelegate(PlayerSettlementBehaviour.game_menu_rebuild_condition), new GameMenuOption.OnConsequenceDelegate(PlayerSettlementBehaviour.game_menu_rebuild_consequence), false, -1, false, null);
                campaignGameSystemStarter.AddGameMenuOption("castle", "leave_rebuild", "{=player_settlement_41}Rebuild Settlement", new GameMenuOption.OnConditionDelegate(PlayerSettlementBehaviour.game_menu_rebuild_condition), new GameMenuOption.OnConsequenceDelegate(PlayerSettlementBehaviour.game_menu_rebuild_consequence), false, -1, false, null);
                campaignGameSystemStarter.AddGameMenuOption("village", "leave_rebuild", "{=player_settlement_41}Rebuild Settlement", new GameMenuOption.OnConditionDelegate(PlayerSettlementBehaviour.game_menu_rebuild_condition), new GameMenuOption.OnConsequenceDelegate(PlayerSettlementBehaviour.game_menu_rebuild_consequence), false, -1, false, null);
            }
            catch (Exception e)
            {
                LogManager.Log.NotifyBad(e);
            }

        }

        private static void game_menu_rebuild_consequence(MenuCallbackArgs args)
        {
            conv_rebuild_consequence();
            try
            {
                PlayerEncounter.LeaveSettlement();
                PlayerEncounter.Finish(true);
                //Campaign.Current.SaveHandler.SignalAutoSave();
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.UnstoppableFastForward;
            }
            catch (Exception e)
            {
                LogManager.EventTracer.Trace(new List<string> { e.Message, e.StackTrace });
            }
        }

        private static bool game_menu_rebuild_condition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            if (MobileParty.MainParty?.Army != null || Main.Settings == null || Main.Settings.ImmersiveMode)
            {
                return false;
            }

            var result = conv_rebuild_condition(true);
            if (!result)
            {
                return result;
            }

            args.IsEnabled = conv_rebuild_clickable(out TextObject reason, true);
            args.Tooltip = reason;
            return result;
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

            // TODO: Determine replacement?
            //Campaign.Current.autoEnterTown = null;

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
            _playerSettlementInfo ??= new PlayerSettlementInfo();
            PlayerSettlementInfo.Instance = _playerSettlementInfo;

            PlayerSettlementInfo.Instance.OnLoad();

            HasLoaded = true;
        }

        private void DailyTick()
        {
            try
            {
                LogManager.EventTracer.Trace();

                if (PlayerSettlementBehaviour.OldSaveLoaded)
                {
                    TextObject message = new TextObject("{=player_settlement_08}A player town has been created on a later save. Older saves are not supported and could cause save corruption or town 'ghosting'.", null);
                    MBInformationManager.AddQuickInformation(message, 0, null);
                    LogManager.Log.NotifyBad(message.ToString());
                    PlayerSettlementBehaviour.OldSaveLoaded = false;
                    return;
                }
                if (PlayerSettlementBehaviour.TriggerSaveLoadAfterUpgrade)
                {
                    PlayerSettlementBehaviour.TriggerSaveLoadAfterUpgrade = false;
                    SaveHandler.SaveLoad(overwrite: true);
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
                        var extraVillages = PlayerSettlementInfo.Instance.PlayerVillages;
                        if (extraVillages == null)
                        {
                            extraVillages = (PlayerSettlementInfo.Instance.PlayerVillages = new List<PlayerSettlementItem>());
                        }

                        var overwrites = PlayerSettlementInfo.Instance.OverwriteSettlements;
                        if (overwrites == null)
                        {
                            overwrites = (PlayerSettlementInfo.Instance.OverwriteSettlements = new());
                        }

                        for (int t = 0; t < extraVillages.Count; t++)
                        {
                            var village = extraVillages[t];
                            if (!village.BuildComplete && !village.BuildEnd.IsFuture)
                            {
                                NotifyComplete(village);
                            }
                        }

                        for (int t = 0; t < overwrites.Count; t++)
                        {
                            var overwrite = overwrites[t];
                            if (!overwrite.BuildComplete && !overwrite.BuildEnd.IsFuture)
                            {
                                NotifyComplete(overwrite);
                            }
                        }

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
            catch (Exception e)
            {
                LogManager.Log.NotifyBad(e);
            }
        }

        public void NotifyComplete(ISettlementItem item)
        {
            item.SetBuildComplete(true);
            TextObject message = new TextObject("{=player_settlement_07}{TOWN} construction has completed!", null);
            message.SetTextVariable("TOWN", item.GetSettlementName());
            MBInformationManager.AddQuickInformation(message, 0, null);
            LogManager.Log.NotifyGood(message.ToString());

            _settlementBuildComplete.Invoke(item.GetSettlement()!);
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
        }

        public void OnBeforeTick(ref MapCameraView.InputInformation inputInformation)
        {
            if (Main.Settings != null && Main.Settings.Enabled && PlayerSettlementInfo.Instance != null)
            {
                if (Game.Current.GameStateManager.ActiveState is not MapState mapState)
                {
                    return;
                }
                if (mapState.Handler is not MapScreen mapScreen)
                {
                    return;
                }

                if (ghostGateVisualEntity != null)
                {

                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                    Campaign.Current.SetTimeControlModeLock(true);

                    if (mapScreen.Input.IsKeyReleased(Main.Submodule!.HelpKey.GetInputKey()))
                    {
                        ShowGatePosHelp(forceShow: true);
                    }

                    Vec3 worldMouseNear = Vec3.Zero;
                    Vec3 worldMouseFar = Vec3.Zero;
                    mapScreen.SceneLayer.SceneView.TranslateMouse(ref worldMouseNear, ref worldMouseFar);
                    Vec3 clippedMouseNear = worldMouseNear;
                    Vec3 clippedMouseFar = worldMouseFar;
                    PathFaceRecord currentFace = PathFaceRecord.NullFaceRecord;
                    mapScreen.GetCursorIntersectionPoint(ref clippedMouseNear, ref clippedMouseFar, out float closestDistanceSquared, out Vec3 _, ref currentFace, out bool isOnLand);
                    mapScreen.GetCursorIntersectionPoint(ref clippedMouseNear, ref clippedMouseFar, out float _, out Vec3 intersectionPoint2, ref currentFace, out isOnLand, BodyFlags.Disabled | BodyFlags.Moveable | BodyFlags.AILimiter | BodyFlags.Barrier | BodyFlags.Barrier3D | BodyFlags.Ragdoll | BodyFlags.RagdollLimiter | BodyFlags.DoNotCollideWithRaycast);
                    MatrixFrame identity = MatrixFrame.Identity;
                    identity.origin = intersectionPoint2;
                    identity.Scale(new Vec3(0.25f, 0.25f, 0.25f));

                    var previous = ghostGateVisualEntity.GetFrame();

                    gatePlacementFrame = identity;
                    this.SetFrame(ghostGateVisualEntity, ref identity);

                    bool flag = currentFace.IsValid() && isOnLand && currentFace.FaceIslandIndex == MobileParty.MainParty.CurrentNavigationFace.FaceIslandIndex; // Campaign.Current.MapSceneWrapper.AreFacesOnSameIsland(currentFace, MobileParty.MainParty.CurrentNavigationFace, ignoreDisabled: false);
                    mapScreen.SceneLayer.ActiveCursor = (flag ? CursorType.Default : CursorType.Disabled);

                    return;
                }


                var deepEditChanged = mapScreen.SceneLayer.Input.IsKeyReleased(Main.Submodule!.DeepEditToggleKey.GetInputKey());
                if (IsPlacingSettlement && deepEditChanged)
                {
                    ToggleDeepEdit();
                    return;
                }

                if (IsPlacingSettlement)
                {

                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                    Campaign.Current.SetTimeControlModeLock(true);

                    if (mapScreen.Input.IsKeyReleased(Main.Submodule!.HelpKey.GetInputKey()))
                    {
                        if (IsDeepEdit)
                        {
                            ShowDeepEditHelp(forceShow: true);
                        }
                        else
                        {
                            ShowSettlementPlacementHelp(forceShow: true);
                        }
                    }

                    if (deepEditPrefab == settlementVisualPrefab)
                    {
                        foreach (var dte in deepTransformEdits)
                        {
                            var entity = dte.Index < 0 ? settlementVisualEntity : settlementVisualEntityChildren[dte.Index];
                            var local = entity!.GetFrame();
                            local.rotation = dte?.Transform?.RotationScale != null ? dte.Transform.RotationScale : local.rotation;
                            if (dte!.Index >= 0)
                            {
                                local.origin = dte?.Transform?.Position != null ? dte.Transform.Position : local.origin;
                            }
                            // No else statement. Offsets do not get applied here otherwise it would cause infinite motion. Rather, offsets are calculated at the end.

                            this.SetFrame(entity, ref local, atGround: false);

                            if (dte?.IsDeleted ?? false)
                            {
                                entity.SetVisibilityExcludeParents(false);
                            }
                            else if (entity != null)
                            {
                                entity.SetVisibilityExcludeParents(true);
                            }
                        }
                    }

                    if (IsDeepEdit && currentDeepTarget != null && !currentDeepTarget.IsVisibleIncludeParents())
                    {
                        UpdateDeepTarget(forward: true);
                        return;
                    }

                    if (mapScreen.Input.IsKeyDown(Main.Submodule!.DeepEditApplyKey.GetInputKey()))
                    {
                        if (IsDeepEdit)
                        {
                            // Don't show help as gate placement will show its own, or apply will occur directly
                            ToggleDeepEdit(showHelp: false);

                            if (deepEditPrefab == settlementVisualPrefab)
                            {
                                foreach (var dte in deepTransformEdits)
                                {
                                    var entity = dte.Index < 0 ? settlementVisualEntity : settlementVisualEntityChildren[dte.Index];
                                    var local = entity!.GetFrame();
                                    local.rotation = dte?.Transform?.RotationScale != null ? dte.Transform.RotationScale : local.rotation;
                                    if (dte!.Index >= 0)
                                    {
                                        local.origin = dte?.Transform?.Position != null ? dte.Transform.Position : local.origin;
                                    }
                                    else
                                    {
                                        // Offsets must be applied here as the next phase wont reapply or recalculate for offsets
                                        local.origin = dte?.Transform?.Offsets != null ? local.origin + dte.Transform.Offsets : local.origin;
                                    }

                                    entity.SetFrame(ref local);

                                    if (dte?.IsDeleted ?? false)
                                    {
                                        entity.SetVisibilityExcludeParents(false);
                                    }
                                    else if (entity != null)
                                    {
                                        entity.SetVisibilityExcludeParents(true);
                                    }
                                }
                            }

                            StartGatePlacement();
                            return;
                        }
                    }

                    var scaleModifierDown = mapScreen.SceneLayer.Input.IsKeyDown(Main.Submodule!.ScaleModifierKey.GetInputKey());
                    var cycleModifierDown = mapScreen.SceneLayer.Input.IsKeyDown(Main.Submodule!.CycleModifierKey.GetInputKey());

                    if (scaleModifierDown)
                    {
                        var scaleBackRelease = mapScreen.SceneLayer.Input.IsKeyReleased(Main.Submodule!.ScaleSmallerKey.GetInputKey());
                        var scaleForwardRelease = mapScreen.SceneLayer.Input.IsKeyReleased(Main.Submodule!.ScaleBiggerKey.GetInputKey());

                        var scaleBackHeld = mapScreen.SceneLayer.Input.IsKeyDown(Main.Submodule!.ScaleSmallerKey.GetInputKey());
                        var scaleForwardHeld = mapScreen.SceneLayer.Input.IsKeyDown(Main.Submodule!.ScaleBiggerKey.GetInputKey());

                        var holdWait = 1 / Main.Settings.CycleSpeed;

                        if (scaleForwardHeld)
                        {
                            if (holdTime.ApproximatelyEqualsTo(0f))
                            {
                                holdTime = Time.ApplicationTime;
                            }
                            else if ((holdTime + holdWait) < Time.ApplicationTime)
                            {
                                deepEditScale += 0.02f;
                                holdTime = 0f;
                                MarkEdited(currentDeepTarget);
                                return;
                            }
                        }
                        else if (scaleBackHeld)
                        {
                            if (holdTime.ApproximatelyEqualsTo(0f))
                            {
                                holdTime = Time.ApplicationTime;
                            }
                            else if ((holdTime + holdWait) < Time.ApplicationTime)
                            {
                                deepEditScale -= 0.02f;
                                if (deepEditScale <= 0.15f)
                                {
                                    deepEditScale = 0.1f;
                                }
                                holdTime = 0f;
                                MarkEdited(currentDeepTarget);
                                return;
                            }
                        }
                        else
                        {
                            holdTime = 0f;
                        }

                        if (scaleForwardRelease && holdTime.ApproximatelyEqualsTo(0f))
                        {
                            deepEditScale += 0.02f;
                            MarkEdited(currentDeepTarget);
                            return;
                        }
                        else if (scaleBackRelease && holdTime.ApproximatelyEqualsTo(0f))
                        {
                            deepEditScale -= 0.02f;
                            if (deepEditScale <= 0.15f)
                            {
                                deepEditScale = 0.1f;
                            }
                            MarkEdited(currentDeepTarget);
                            return;
                        }
                    }
                    else if (cycleModifierDown)
                    {
                        var cycleBackRelease = mapScreen.SceneLayer.Input.IsKeyReleased(Main.Submodule!.CycleBackKey.GetInputKey());
                        var cycleForwardRelease = mapScreen.SceneLayer.Input.IsKeyReleased(Main.Submodule!.CycleNextKey.GetInputKey());

                        var cycleBackHeld = mapScreen.SceneLayer.Input.IsKeyDown(Main.Submodule!.CycleBackKey.GetInputKey());
                        var cycleForwardHeld = mapScreen.SceneLayer.Input.IsKeyDown(Main.Submodule!.CycleNextKey.GetInputKey());

                        var moveUpRelease = mapScreen.SceneLayer.Input.IsKeyReleased(Main.Submodule!.MoveUpKey.GetInputKey());
                        var moveDownRelease = mapScreen.SceneLayer.Input.IsKeyReleased(Main.Submodule!.MoveDownKey.GetInputKey());

                        var moveUpHeld = mapScreen.SceneLayer.Input.IsKeyDown(Main.Submodule!.MoveUpKey.GetInputKey());
                        var moveDownHeld = mapScreen.SceneLayer.Input.IsKeyDown(Main.Submodule!.MoveDownKey.GetInputKey());

                        var holdWait = 1 / Main.Settings.CycleSpeed;
                        if (IsDeepEdit)
                        {
                            if (cycleForwardHeld)
                            {
                                if (holdTime.ApproximatelyEqualsTo(0f))
                                {
                                    holdTime = Time.ApplicationTime;
                                }
                                else if ((holdTime + holdWait) < Time.ApplicationTime)
                                {
                                    UpdateDeepTarget(true);
                                    holdTime = 0f;
                                    return;
                                }
                            }
                            else if (cycleBackHeld)
                            {
                                if (holdTime.ApproximatelyEqualsTo(0f))
                                {
                                    holdTime = Time.ApplicationTime;
                                }
                                else if ((holdTime + holdWait) < Time.ApplicationTime)
                                {
                                    UpdateDeepTarget(false);
                                    holdTime = 0f;
                                    return;
                                }
                            }
                            else if (moveUpHeld)
                            {
                                if (holdTime.ApproximatelyEqualsTo(0f))
                                {
                                    holdTime = Time.ApplicationTime;
                                }
                                else if ((holdTime + holdWait) < Time.ApplicationTime)
                                {
                                    var editModifier = 0.02f;
                                    var edited = MarkEdited(currentDeepTarget);
                                    if (currentDeepTarget != settlementVisualEntity && edited?.Transform?.Position != null)
                                    {
                                        edited.Transform.Position.z += editModifier;
                                    }
                                    else if (currentDeepTarget == settlementVisualEntity)
                                    {
                                        if (edited?.Transform != null)
                                        {
                                            if (edited.Transform.Offsets == null)
                                            {
                                                edited.Transform.Offsets = Vec3.Zero;
                                            }
                                            edited.Transform.Offsets!.z += editModifier;
                                        }
                                    }
                                    holdTime = 0f;
                                    return;
                                }
                            }
                            else if (moveDownHeld)
                            {
                                if (holdTime.ApproximatelyEqualsTo(0f))
                                {
                                    holdTime = Time.ApplicationTime;
                                }
                                else if ((holdTime + holdWait) < Time.ApplicationTime)
                                {
                                    var editModifier = 0.02f;
                                    var edited = MarkEdited(currentDeepTarget);
                                    if (currentDeepTarget != settlementVisualEntity && edited?.Transform?.Position != null)
                                    {
                                        edited.Transform.Position.z -= editModifier;
                                    }
                                    else if (currentDeepTarget == settlementVisualEntity)
                                    {
                                        if (edited?.Transform != null)
                                        {
                                            if (edited.Transform.Offsets == null)
                                            {
                                                edited.Transform.Offsets = Vec3.Zero;
                                            }
                                            edited.Transform.Offsets!.z -= editModifier;
                                        }
                                    }
                                    holdTime = 0f;
                                    return;
                                }
                            }
                            else
                            {
                                holdTime = 0f;
                            }

                            if (cycleForwardRelease && cycleModifierDown && holdTime.ApproximatelyEqualsTo(0f))
                            {
                                UpdateDeepTarget(true);
                                return;
                            }
                            else if (cycleBackRelease && cycleModifierDown && holdTime.ApproximatelyEqualsTo(0f))
                            {
                                UpdateDeepTarget(false);
                                return;
                            }
                            else if (moveUpRelease && cycleModifierDown && holdTime.ApproximatelyEqualsTo(0f))
                            {
                                var editModifier = 0.02f;
                                var edited = MarkEdited(currentDeepTarget);
                                if (currentDeepTarget != settlementVisualEntity && edited?.Transform?.Position != null)
                                {
                                    edited.Transform.Position.z += editModifier;
                                }
                                else if (currentDeepTarget == settlementVisualEntity)
                                {
                                    if (edited?.Transform != null)
                                    {
                                        if (edited.Transform.Offsets == null)
                                        {
                                            edited.Transform.Offsets = Vec3.Zero;
                                        }
                                        edited.Transform.Offsets!.z += editModifier;
                                    }
                                }
                                return;
                            }
                            else if (moveDownRelease && cycleModifierDown && holdTime.ApproximatelyEqualsTo(0f))
                            {
                                var editModifier = 0.02f;
                                var edited = MarkEdited(currentDeepTarget);
                                if (currentDeepTarget != settlementVisualEntity && edited?.Transform?.Position != null)
                                {
                                    edited.Transform.Position.z -= editModifier;
                                }
                                else if (currentDeepTarget == settlementVisualEntity)
                                {
                                    if (edited?.Transform != null)
                                    {
                                        if (edited.Transform.Offsets == null)
                                        {
                                            edited.Transform.Offsets = Vec3.Zero;
                                        }
                                        edited.Transform.Offsets!.z -= editModifier;
                                    }
                                }
                                return;
                            }
                        }
                        else
                        {
                            if (cycleForwardHeld)
                            {
                                if (holdTime.ApproximatelyEqualsTo(0f))
                                {
                                    holdTime = Time.ApplicationTime;
                                }
                                else if ((holdTime + holdWait) < Time.ApplicationTime)
                                {
                                    UpdateSettlementVisualEntity(true);
                                    holdTime = 0f;
                                    return;
                                }
                            }
                            else if (cycleBackHeld)
                            {
                                if (holdTime.ApproximatelyEqualsTo(0f))
                                {
                                    holdTime = Time.ApplicationTime;
                                }
                                else if ((holdTime + holdWait) < Time.ApplicationTime)
                                {
                                    UpdateSettlementVisualEntity(false);
                                    holdTime = 0f;
                                    return;
                                }
                            }
                            else if (moveUpHeld)
                            {
                                if (holdTime.ApproximatelyEqualsTo(0f))
                                {
                                    holdTime = Time.ApplicationTime;
                                }
                                else if ((holdTime + holdWait) < Time.ApplicationTime)
                                {
                                    var editModifier = 0.02f;
                                    var edited = MarkEdited(settlementVisualEntity);

                                    // Not deep edit, only applies for root
                                    if (edited?.Transform != null)
                                    {
                                        if (edited.Transform.Offsets == null)
                                        {
                                            edited.Transform.Offsets = Vec3.Zero;
                                        }
                                        edited.Transform.Offsets!.z += editModifier;
                                    }

                                    holdTime = 0f;
                                    return;
                                }
                            }
                            else if (moveDownHeld)
                            {
                                if (holdTime.ApproximatelyEqualsTo(0f))
                                {
                                    holdTime = Time.ApplicationTime;
                                }
                                else if ((holdTime + holdWait) < Time.ApplicationTime)
                                {
                                    var editModifier = 0.02f;
                                    var edited = MarkEdited(settlementVisualEntity);
                                    // Not deep edit, only applies for root
                                    if (edited?.Transform != null)
                                    {
                                        if (edited.Transform.Offsets == null)
                                        {
                                            edited.Transform.Offsets = Vec3.Zero;
                                        }
                                        edited.Transform.Offsets!.z -= editModifier;
                                    }

                                    holdTime = 0f;
                                    return;
                                }
                            }
                            else
                            {
                                holdTime = 0f;
                            }

                            if (cycleForwardRelease && cycleModifierDown && holdTime.ApproximatelyEqualsTo(0f))
                            {
                                UpdateSettlementVisualEntity(true);
                                return;
                            }
                            else if (cycleBackRelease && cycleModifierDown && holdTime.ApproximatelyEqualsTo(0f))
                            {
                                UpdateSettlementVisualEntity(false);
                                return;
                            }
                            else if (moveUpRelease && cycleModifierDown && holdTime.ApproximatelyEqualsTo(0f))
                            {
                                var editModifier = 0.02f;
                                var edited = MarkEdited(settlementVisualEntity);
                                // Not deep edit, only applies for root
                                if (edited?.Transform != null)
                                {
                                    if (edited.Transform.Offsets == null)
                                    {
                                        edited.Transform.Offsets = Vec3.Zero;
                                    }
                                    edited.Transform.Offsets!.z += editModifier;
                                }
                                return;
                            }
                            else if (moveDownRelease && cycleModifierDown && holdTime.ApproximatelyEqualsTo(0f))
                            {
                                var editModifier = 0.02f;
                                var edited = MarkEdited(settlementVisualEntity);
                                // Not deep edit, only applies for root
                                if (edited?.Transform != null)
                                {
                                    if (edited.Transform.Offsets == null)
                                    {
                                        edited.Transform.Offsets = Vec3.Zero;
                                    }
                                    edited.Transform.Offsets!.z -= editModifier;
                                }

                                return;
                            }
                        }
                    }
                    else
                    {
                        holdTime = 0f;
                    }


                    if (IsDeepEdit)
                    {
                        var undeleteModifierDown = mapScreen.SceneLayer.Input.IsKeyDown(Main.Submodule!.UnDeleteModifierKey.GetInputKey());
                        var deleteRelease = mapScreen.SceneLayer.Input.IsKeyReleased(Main.Submodule!.DeleteKey.GetInputKey());
                        if (deleteRelease)
                        {
                            if (undeleteModifierDown)
                            {
                                var edited = deepTransformEdits.LastOrDefault(d => d.IsDeleted && d.Index >= 0);

                                if (edited != null)
                                {
                                    edited.IsDeleted = false;
                                }
                                UpdateDeepTarget(currentDeepTarget);
                                return;
                            }
                            else if (currentDeepTarget != settlementVisualEntity)
                            {
                                if (currentDeepTarget?.IsVisibleIncludeParents() ?? false)
                                {
                                    var edited = MarkEdited(currentDeepTarget);

                                    if (edited != null)
                                    {
                                        edited.IsDeleted = true;
                                    }
                                    UpdateDeepTarget(forward: true);
                                    return;
                                }
                            }
                        }
                    }


                    var previous = settlementVisualEntity!.GetFrame();
                    PathFaceRecord currentFace = PathFaceRecord.NullFaceRecord;

                    MatrixFrame identity = previous; //MatrixFrame.Identity;

                    // When not in deep edit, the settlement visual must follow the mouse
                    if (!IsDeepEdit)
                    {
                        Vec3 worldMouseNear = Vec3.Zero;
                        Vec3 worldMouseFar = Vec3.Zero;
                        mapScreen.SceneLayer.SceneView.TranslateMouse(ref worldMouseNear, ref worldMouseFar);
                        Vec3 clippedMouseNear = worldMouseNear;
                        Vec3 clippedMouseFar = worldMouseFar;
                        mapScreen.GetCursorIntersectionPoint(ref clippedMouseNear, ref clippedMouseFar, out float closestDistanceSquared, out Vec3 _, ref currentFace, out bool isOnLand);
                        mapScreen.GetCursorIntersectionPoint(ref clippedMouseNear, ref clippedMouseFar, out float _, out Vec3 intersectionPoint2, ref currentFace, out isOnLand, BodyFlags.Disabled | BodyFlags.Moveable | BodyFlags.AILimiter | BodyFlags.Barrier | BodyFlags.Barrier3D | BodyFlags.Ragdoll | BodyFlags.RagdollLimiter | BodyFlags.DoNotCollideWithRaycast);


                        identity.origin = intersectionPoint2;
                    }

                    var settlementDte = deepTransformEdits.FirstOrDefault(dte => dte.Index < 0);

                    this.SetFrame(settlementVisualEntity, ref identity, atGround: true, settlementDte?.Transform?.Offsets);


                    var editTarget = currentDeepTarget ?? settlementVisualEntity;

                    MatrixFrame frame = editTarget!.GetGlobalFrame();


                    var rotateSidesVelocity = 0f;
                    var rotateForwardVelocity = 0f;
                    var rotateModifierDown = mapScreen.SceneLayer.Input.IsKeyDown(Main.Submodule!.RotateModifierKey.GetInputKey());
                    var rotateExtraModifierDown = mapScreen.SceneLayer.Input.IsKeyDown(Main.Submodule!.RotateAlternateModifierKey.GetInputKey());

                    var rotateLeftKeyDown = mapScreen.SceneLayer.Input.IsKeyDown(Main.Submodule!.RotatePreviousKey.GetInputKey());
                    var rotateRightKeyDown = mapScreen.SceneLayer.Input.IsKeyDown(Main.Submodule!.RotateNextKey.GetInputKey());

                    var rotateForwardKeyDown = mapScreen.SceneLayer.Input.IsKeyDown(Main.Submodule!.RotateForwardKey.GetInputKey());
                    var rotateBackwardKeyDown = mapScreen.SceneLayer.Input.IsKeyDown(Main.Submodule!.RotateBackwardsKey.GetInputKey());

                    // When in deep edit, the targeted model must follow the mouse when both rotate modifier and left mouse is down/held
                    if (IsDeepEdit && rotateModifierDown && mapScreen.SceneLayer.Input.IsKeyDown(TaleWorlds.InputSystem.InputKey.LeftMouseButton))
                    {
                        Vec3 worldMouseNear = Vec3.Zero;
                        Vec3 worldMouseFar = Vec3.Zero;
                        mapScreen.SceneLayer.SceneView.TranslateMouse(ref worldMouseNear, ref worldMouseFar);
                        Vec3 clippedMouseNear = worldMouseNear;
                        Vec3 clippedMouseFar = worldMouseFar;
                        mapScreen.GetCursorIntersectionPoint(ref clippedMouseNear, ref clippedMouseFar, out float closestDistanceSquared, out Vec3 _, ref currentFace, out bool isOnLand);
                        mapScreen.GetCursorIntersectionPoint(ref clippedMouseNear, ref clippedMouseFar, out float _, out Vec3 intersectionPoint2, ref currentFace, out isOnLand, BodyFlags.Disabled | BodyFlags.Moveable | BodyFlags.AILimiter | BodyFlags.Barrier | BodyFlags.Barrier3D | BodyFlags.Ragdoll | BodyFlags.RagdollLimiter | BodyFlags.DoNotCollideWithRaycast);


                        frame.origin.x = intersectionPoint2.x;
                        frame.origin.y = intersectionPoint2.y;
                        MarkEdited(editTarget);
                    }

                    if (rotateForwardKeyDown && rotateModifierDown)
                    {
                        rotateForwardVelocity = inputInformation.Dt * 2f;
                        MarkEdited(editTarget);
                    }
                    else if (rotateBackwardKeyDown && rotateModifierDown)
                    {
                        rotateForwardVelocity = inputInformation.Dt * -2f;
                        MarkEdited(editTarget);
                    }
                    rotateForwardVelocity = rotateForwardVelocity * 2.75f * inputInformation.Dt;

                    if (rotateLeftKeyDown && rotateModifierDown)
                    {
                        rotateSidesVelocity = inputInformation.Dt * 2f;
                        MarkEdited(editTarget);
                    }
                    else if (rotateRightKeyDown && rotateModifierDown)
                    {
                        rotateSidesVelocity = inputInformation.Dt * -2f;
                        MarkEdited(editTarget);
                    }
                    rotateSidesVelocity = rotateSidesVelocity + inputInformation.HorizontalCameraInput * 1.75f * inputInformation.Dt;
                    if (inputInformation.RightMouseButtonDown && rotateModifierDown)
                    {
                        rotateSidesVelocity += 0.01f * inputInformation.MouseSensitivity * inputInformation.MouseMoveX;

                        // Divide by 5 for actual settings
                        rotateSidesVelocity *= (Main.Settings.MouseRotationModifier / 5);
                        MarkEdited(editTarget);
                    }
                    else if (rotateModifierDown && (rotateLeftKeyDown || rotateRightKeyDown))
                    {
                        // Divide by 5 for actual settings
                        rotateSidesVelocity *= (Main.Settings.KeyRotationModifier / 5);
                        MarkEdited(editTarget);
                    }


                    var sidesBearing = rotateSidesVelocity;
                    frame.rotation.RotateAboutUp(-sidesBearing);

                    if (rotateExtraModifierDown)
                    {
                        var forwardBearing = rotateForwardVelocity;
                        frame.rotation.RotateAboutSide(-forwardBearing);
                    }
                    else
                    {
                        var forwardBearing = rotateForwardVelocity;
                        frame.rotation.RotateAboutForward(-forwardBearing);
                    }
                    frame.Scale(new Vec3(deepEditScale, deepEditScale, deepEditScale));

                    // Reset otherwise scale gets infinitely affected
                    deepEditScale = 1f;

                    editTarget.SetGlobalFrame(frame);

                    var edit = deepTransformEdits.FirstOrDefault(dte => dte.Index == settlementVisualEntityChildren.IndexOf(editTarget));
                    if (edit != null)
                    {
                        var lFrame = editTarget.GetFrame();
                        edit.Transform = new TransformSaveable
                        {
                            Position = edit.Index < 0 ? null : lFrame.origin,
                            Offsets = edit.Index < 0 ? lFrame.origin - identity.origin : null,
                            RotationScale = lFrame.rotation
                        };
                    }


                    // Store last frame after all edits
                    settlementPlacementFrame = settlementVisualEntity.GetFrame();

                    if (!IsDeepEdit)
                    {
                        bool flag = currentFace.IsValid() && currentFace.FaceIslandIndex == MobileParty.MainParty.CurrentNavigationFace.FaceIslandIndex; // Campaign.Current.MapSceneWrapper.AreFacesOnSameIsland(currentFace, MobileParty.MainParty.CurrentNavigationFace, ignoreDisabled: false);
                        mapScreen.SceneLayer.ActiveCursor = (flag ? CursorType.Default : CursorType.Disabled);
                    }

                }
            }
        }

        private static void ShowDeepEditHelp(bool forceShow = false)
        {
            if (Main.Settings!.DisableAutoHints && !forceShow)
            {
                return;
            }

            TextObject deepEditMessage = new TextObject("{=player_settlement_38}Press {HELP_KEY} for help. \r\nPress {APPLY_KEY} to apply or press {ESC_KEY} to cancel.  \r\nUse {DEEP_EDIT_KEY} to switch from deep edit mode to placement mode. \r\nUse {CYCLE_MODIFIER_KEY} and {CYCLE_BACK_KEY} / {CYCLE_NEXT_KEY} to change selected sub model.\r\nUse {ROTATE_MODIFIER_KEY} and {MOUSE_CLICK} to reposition. \r\nUse {ROTATE_MODIFIER_KEY} and {ROTATE_BACK_KEY} / {ROTATE_NEXT_KEY} to change rotation. \r\nUse {ROTATE_MODIFIER_KEY} and {ROTATE_FORWARD_KEY} / {ROTATE_BACKWARD_KEY} to change forward rotation. \r\nUse {ROTATE_MODIFIER_KEY} + {ROTATE_MODIFIER_ALTERNATE} and {ROTATE_FORWARD_KEY} / {ROTATE_BACKWARD_KEY} to change axis rotation. \r\nUse {SCALE_MODIFIER_KEY} and {SCALE_BACK_KEY} / {SCALE_NEXT_KEY} to change scale. \r\nUse {CYCLE_MODIFIER_KEY} and {MOVE_UP_KEY} / {MOVE_DOWN_KEY} to move up or down. \r\nUse {DELETE_KEY} to delete selection. \r\nUse {UNDELETE_MODIFIER_KEY} and {DELETE_KEY} to un-delete previous deletion.");
            deepEditMessage.SetTextVariable("HELP_KEY", TaleWorlds.Core.HyperlinkTexts.GetKeyHyperlinkText(Main.Submodule!.HelpKey.GetInputKey().ToString()));
            deepEditMessage.SetTextVariable("ESC_KEY", TaleWorlds.Core.HyperlinkTexts.GetKeyHyperlinkText(InputKey.Escape.ToString()));
            deepEditMessage.SetTextVariable("APPLY_KEY", TaleWorlds.Core.HyperlinkTexts.GetKeyHyperlinkText(Main.Submodule!.DeepEditApplyKey.GetInputKey().ToString()));
            deepEditMessage.SetTextVariable("DEEP_EDIT_KEY", TaleWorlds.Core.HyperlinkTexts.GetKeyHyperlinkText(Main.Submodule!.DeepEditToggleKey.GetInputKey().ToString()));
            deepEditMessage.SetTextVariable("CYCLE_MODIFIER_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.CycleModifierKey).ToString()));
            deepEditMessage.SetTextVariable("CYCLE_BACK_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.CycleBackKey).ToString()));
            deepEditMessage.SetTextVariable("CYCLE_NEXT_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.CycleNextKey).ToString()));
            deepEditMessage.SetTextVariable("MOVE_UP_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.MoveUpKey).ToString()));
            deepEditMessage.SetTextVariable("MOVE_DOWN_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.MoveDownKey).ToString()));
            deepEditMessage.SetTextVariable("MOUSE_CLICK", HyperlinkTexts.GetKeyHyperlinkText(InputKey.LeftMouseButton.ToString()));
            deepEditMessage.SetTextVariable("ROTATE_MODIFIER_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.RotateModifierKey).ToString()));
            deepEditMessage.SetTextVariable("ROTATE_MODIFIER_ALTERNATE", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.RotateAlternateModifierKey).ToString()));
            deepEditMessage.SetTextVariable("ROTATE_BACK_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.RotatePreviousKey).ToString()));
            deepEditMessage.SetTextVariable("ROTATE_NEXT_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.RotateNextKey).ToString()));
            deepEditMessage.SetTextVariable("ROTATE_FORWARD_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.RotateForwardKey).ToString()));
            deepEditMessage.SetTextVariable("ROTATE_BACKWARD_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.RotateBackwardsKey).ToString()));
            deepEditMessage.SetTextVariable("SCALE_MODIFIER_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.ScaleModifierKey).ToString()));
            deepEditMessage.SetTextVariable("SCALE_BACK_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.ScaleSmallerKey).ToString()));
            deepEditMessage.SetTextVariable("SCALE_NEXT_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.ScaleBiggerKey).ToString()));
            deepEditMessage.SetTextVariable("UNDELETE_MODIFIER_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.UnDeleteModifierKey).ToString()));
            deepEditMessage.SetTextVariable("DELETE_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.DeleteKey).ToString()));
            MBInformationManager.AddQuickInformation(deepEditMessage, Main.Settings.HintDurationSeconds * 1000, Hero.MainHero?.CharacterObject);
        }

        private DeepTransformEdit? MarkEdited(GameEntity? currentDeepTarget)
        {
            if (settlementVisualEntity == null || currentDeepTarget == null)
            {
                LogManager.EventTracer.Trace($"Unable to mark edited: settlementVisualEntity = {settlementVisualEntity} currentDeepTarget = {currentDeepTarget}");
                return null;
            }

            if (deepEditPrefab != settlementVisualPrefab)
            {
                deepTransformEdits.Clear();
                deepEditPrefab = settlementVisualPrefab;
            }

            var idx = settlementVisualEntityChildren.IndexOf(currentDeepTarget);

            var edit = deepTransformEdits.FirstOrDefault(dte => dte.Index == idx);
            if (edit == null)
            {
                var frame = currentDeepTarget.GetFrame();
                edit = new DeepTransformEdit
                {
                    Index = idx,
                    Name = currentDeepTarget.Name,
                    Transform = new TransformSaveable
                    {
                        Position = frame.origin,
                        Offsets = Vec3.Zero,
                        RotationScale = frame.rotation
                    },
                    IsDeleted = false
                };
                deepTransformEdits.Add(edit);
            }
            LogManager.EventTracer.Trace($"Mark edited: settlementVisualEntity = {settlementVisualEntity?.Name} currentDeepTarget = {currentDeepTarget?.Name} idx = {idx}");
            return edit;
        }

        private void ToggleDeepEdit(bool showHelp = true)
        {
            deepEdit = !deepEdit;
            if (deepEdit)
            {
                if (showHelp)
                {
                    ShowDeepEditHelp();
                }

                deepEditPrefab = settlementVisualPrefab;
                UpdateDeepTarget(settlementVisualEntity);
            }
            else
            {
                if (showHelp)
                {
                    ShowSettlementPlacementHelp();
                }
                RefreshVisualSelection();
            }
        }

        private void UpdateDeepTarget(bool forward)
        {
            bool foundValidTarget = false;
            if (forward)
            {
                var idx = settlementVisualEntityChildren.IndexOf(currentDeepTarget!);
                if (idx < 0)
                {
                    // Currently on root, go to first child.
                    idx = 0;

                    do
                    {
                        if (UpdateDeepTarget(idx))
                        {
                            foundValidTarget = true;
                            break;
                        }
                        else
                        {
                            idx += 1;
                            if (idx == settlementVisualEntityChildren.Count)
                            {
                                UpdateDeepTarget(-1);
                                return;
                            }
                        }
                    } while (!foundValidTarget);

                    return;
                }


                do
                {
                    idx += 1;

                    if (idx == settlementVisualEntityChildren.Count)
                    {
                        // Reached final child, loop around to root
                        UpdateDeepTarget(-1);
                        return;
                    }

                    if (UpdateDeepTarget(idx))
                    {
                        foundValidTarget = true;
                        break;
                    }
                } while (!foundValidTarget);

                return;
            }
            else
            {
                var idx = settlementVisualEntityChildren.IndexOf(currentDeepTarget!);
                if (idx < 0)
                {
                    // Currently on root, go to last child.
                    idx = settlementVisualEntityChildren.Count - 1;

                    do
                    {
                        if (UpdateDeepTarget(idx))
                        {
                            foundValidTarget = true;
                            break;
                        }
                        else
                        {
                            idx -= 1;
                            if (idx < 0)
                            {
                                UpdateDeepTarget(-1);
                                return;
                            }
                        }
                    } while (!foundValidTarget);

                    return;
                }


                do
                {
                    idx -= 1;

                    if (idx < 0)
                    {
                        // Reached first child, loop around to root
                        UpdateDeepTarget(-1);
                        return;
                    }

                    if (UpdateDeepTarget(idx))
                    {
                        foundValidTarget = true;
                        break;
                    }
                } while (!foundValidTarget);

                return;
            }
        }

        private bool UpdateDeepTarget(GameEntity? target)
        {

            if (target == null)
            {
                ResetDeepEdits();
                return false;
            }

            var idx = settlementVisualEntityChildren.IndexOf(target);

            return UpdateDeepTarget(idx);
        }

        private bool UpdateDeepTarget(int idx)
        {
            RefreshVisualSelection();

            HideDeletedDeepEdits();

            GameEntity? target = idx < 0 ? settlementVisualEntity : settlementVisualEntityChildren[idx];

            currentDeepTarget = target;

            if (!(currentDeepTarget?.IsVisibleIncludeParents() ?? false))
            {
                return false;
            }

            // Recursively highlight target and submodels as green to indicate selection
            bool UpdateEntities(GameEntity parent)
            {
                bool foundMesh = false;
                var childEntities = parent!.GetEntityAndChildren().ToList();

                for (int j = 0; j < childEntities.Count; j++)
                {
                    GameEntity entity = childEntities[j];

                    if (entity != parent)
                    {
                        foundMesh = UpdateEntities(entity) || foundMesh;
                    }

                    MetaMesh? dummyMetaMesh = entity.GetMetaMesh(0);
                    if (dummyMetaMesh == null)
                        continue;

                    for (int i = 0; i < dummyMetaMesh.MeshCount; i++)
                    {
                        Mesh entityMesh = dummyMetaMesh.GetMeshAtIndex(i);
                        var material = entityMesh.GetMaterial();
                        entityMesh.SetMaterial("plain_green");
                        foundMesh = true;
                    }
                }

                return foundMesh;
            }
            return UpdateEntities(target!);
        }

        private void HideDeletedDeepEdits()
        {
            if (deepTransformEdits != null)
            {
                foreach (var dte in deepTransformEdits.Where(dte => dte.IsDeleted && dte.Index >= 0))
                {
                    try
                    {
                        GameEntity? entity = settlementVisualEntityChildren[dte.Index];
                        entity.SetVisibilityExcludeParents(false);
                    }
                    catch (Exception e)
                    {
                        LogManager.EventTracer.Trace(new List<string> { e.Message, e.StackTrace });
                        continue;
                    }
                }
            }
        }

        // // TODO: Might be useful to ensure reachable
        //public Vec3 GetVisualPosition()
        //{
        //    float single = 0f;
        //    Vec2 zero = Vec2.Zero;
        //    Vec2 vec2 = new Vec2(this.PartyBase.GetPosition2D.x + zero.x, this.PartyBase.GetPosition2D.y + zero.y);

        //    return new Vec3(vec2, single, -1f);
        //}

        private void SetFrame(GameEntity? entity, ref MatrixFrame frame, bool atGround = true, Vec3? offset = null)
        {
            if (entity != null /*&& !entity.GetFrame().NearlyEquals(frame, 1E-05f)*/)
            {
                entity.SetFrame(ref frame);

                if (atGround)
                {
                    // Match ground height
                    var mapScene = ((MapScene) Campaign.Current.MapSceneWrapper).Scene;
                    Vec3 vec3 = new Vec3(frame.origin.x, frame.origin.y, 0f, -1f)
                    {
                        z = mapScene.GetGroundHeightAtPosition(new Vec2(frame.origin.x, frame.origin.y).ToVec3(0f), BodyFlags.CommonCollisionExcludeFlags)
                    };
                    frame.origin = vec3;
                    entity.SetLocalPosition(vec3 + (offset ?? Vec3.Zero));
                }
            }
        }

        public void StartGatePlacement()
        {

            if (Main.Settings == null || !Main.Settings.AllowGatePosition || !gateSupported)
            {
                ApplyNow();
                return;
            }

            if (Game.Current.GameStateManager.ActiveState is not MapState mapState)
            {
                return;
            }
            if (mapState.Handler is not MapScreen mapScreen)
            {
                return;
            }

            if (applyPending != null && mapScreen.SceneLayer.Input.GetIsMouseActive() && mapScreen.SceneLayer.ActiveCursor == TaleWorlds.ScreenSystem.CursorType.Default)
            {
                try
                {
                    ShowGatePosHelp();
                    ShowGhostGateVisualEntity(true);
                    return;
                }
                catch (Exception e)
                {
                    LogManager.Log.NotifyBad(e);
                }
            }
        }

        private static void ShowGatePosHelp(bool forceShow = false)
        {
            if (Main.Settings!.DisableAutoHints && !forceShow)
            {
                return;
            }
            TextObject gatePosMessage = new TextObject("{=player_settlement_36}Choose your gate position. \r\nPress {HELP_KEY} for help. \r\nClick {MOUSE_CLICK} anywhere to apply or press {ESC_KEY} to go back to settlement placement.");
            gatePosMessage.SetTextVariable("HELP_KEY", TaleWorlds.Core.HyperlinkTexts.GetKeyHyperlinkText(Main.Submodule!.HelpKey.GetInputKey().ToString()));
            gatePosMessage.SetTextVariable("ESC_KEY", TaleWorlds.Core.HyperlinkTexts.GetKeyHyperlinkText(InputKey.Escape.ToString()));
            gatePosMessage.SetTextVariable("MOUSE_CLICK", HyperlinkTexts.GetKeyHyperlinkText(InputKey.LeftMouseButton.ToString()));
            MBInformationManager.AddQuickInformation(gatePosMessage, Main.Settings.HintDurationSeconds * 1000, Hero.MainHero?.CharacterObject);
        }

        public void ApplyNow()
        {
            if (Game.Current.GameStateManager.ActiveState is not MapState mapState)
            {
                return;
            }
            if (mapState.Handler is not MapScreen mapScreen)
            {
                return;
            }

            if (applyPending != null && mapScreen.SceneLayer.Input.GetIsMouseActive() && mapScreen.SceneLayer.ActiveCursor == TaleWorlds.ScreenSystem.CursorType.Default)
            {
                try
                {
                    var apply = applyPending;
                    apply.Invoke();
                    return;
                }
                catch (Exception e)
                {
                    LogManager.Log.NotifyBad(e);
                }
            }
        }

        public void Reset()
        {
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
            Campaign.Current.SetTimeControlModeLock(false);

            availableModels?.Clear();
            currentModelOptionIdx = -1;

            ClearEntities();

            settlementPlacementFrame = null;
            applyPending = null;
            settlementVisualPrefab = null;

            SettlementRequest = SettlementType.None;
            ReSettlementRequest = null;
            OverwriteRequest = null;

            gateSupported = false;
            ghostGateVisualEntity = null;
            gatePlacementFrame = null;

            ResetDeepEdits();

            settlementVisualEntityChildren.Clear();

            _onReset?.Invoke();
            LogManager.EventTracer.Trace();
        }

        private void ResetDeepEdits()
        {
            deepTransformEdits.Clear();
            deepEditScale = 1f;
            deepEdit = false;
            deepEditPrefab = null;
            currentDeepTarget = null;
        }

        private void ClearEntities()
        {
            settlementVisualPrefab = null;
            settlementVisualEntity?.ClearEntity();
            settlementVisualEntity = null;

            ghostGateVisualEntity?.ClearEntity();
            ghostGateVisualEntity = null;

            LogManager.EventTracer.Trace();
        }

        // TODO: Use to hide parts
        //private void SetSettlementLevelVisibility()
        //{
        //    List<GameEntity> gameEntities = new List<GameEntity>();
        //    this.StrategicEntity.GetChildrenRecursive(ref gameEntities);
        //    foreach (GameEntity gameEntity in gameEntities)
        //    {
        //        if (((uint) gameEntity.GetUpgradeLevelMask() & this._currentLevelMask) != this._currentLevelMask)
        //        {
        //            gameEntity.SetVisibilityExcludeParents(false);
        //            gameEntity.SetPhysicsState(false, true);
        //        }
        //        else
        //        {
        //            gameEntity.SetVisibilityExcludeParents(true);
        //            gameEntity.SetPhysicsState(true, true);
        //        }
        //    }
        //}

        public void Tick(float delta)
        {
            if (Main.Settings != null && Main.Settings.Enabled && PlayerSettlementInfo.Instance != null)
            {
                if (Settlement.CurrentSettlement != null || Hero.MainHero.IsPrisoner || PlayerEncounter.Current != null || Mission.Current != null)
                {
                    // Build will only occur after leaving settlement, being freed, finishing encounter and not being in a mission
                    return;
                }

                if (SettlementRequest == SettlementType.Town)
                {
                    LogManager.EventTracer.Trace("Build requested for Town");

                    Reset();

                    BuildTown();
                    return;
                }
                else if (SettlementRequest == SettlementType.Village)
                {
                    LogManager.EventTracer.Trace("Build requested for Village");

                    Reset();

                    BuildVillage();
                    return;
                }
                else if (SettlementRequest == SettlementType.Castle)
                {
                    LogManager.EventTracer.Trace("Build requested for Castle");

                    Reset();

                    BuildCastle();
                    return;
                }
                else if (ReSettlementRequest != null)
                {
                    var extraInfo = new List<string> { $"Rebuild requested for {ReSettlementRequest.StringId ?? ReSettlementRequest.Identifier.ToString()}" };
                    if (!string.IsNullOrEmpty(ReSettlementRequest.PrefabId))
                    {
                        extraInfo.Add($"Current prefab: ${ReSettlementRequest.PrefabId}");
                    }
                    if (ReSettlementRequest.BuildComplete || !ReSettlementRequest.BuildEnd.IsFuture)
                    {
                        extraInfo.Add($"Build was completed before");
                    }
                    LogManager.EventTracer.Trace(extraInfo);

                    var target = ReSettlementRequest;
                    Reset();

                    target.Settlement!.IsVisible = false;
                    OnResetEvent?.AddNonSerializedListener(target, () =>
                    {
                        // If cancelled, the settlement needs to be shown again
                        OnResetEvent.ClearListeners(target);

                        target.Settlement!.IsVisible = true;
                    });

                    Rebuild(target);
                }
                else if (OverwriteRequest != null)
                {
                    var extraInfo = new List<string> { $"Rebuild requested for {OverwriteRequest.StringId}" };
                    LogManager.EventTracer.Trace(extraInfo);

                    var target = OverwriteRequest;
                    Reset();

                    target.IsVisible = false;
                    OnResetEvent?.AddNonSerializedListener(target, () =>
                    {
                        // If cancelled, the settlement needs to be shown again
                        OnResetEvent.ClearListeners(target);

                        target.IsVisible = true;
                    });

                    Overwrite(target);
                }
            }
        }

        private void Overwrite(Settlement target)
        {
            SettlementType settlementType = target.IsVillage ? SettlementType.Village : target.IsCastle ? SettlementType.Castle : target.IsTown ? SettlementType.Town : SettlementType.None;

            if (settlementType == SettlementType.None)
            {
                Reset();
                return;
            }

            gateSupported = settlementType != SettlementType.Village;

            var title = new TextObject("{=player_settlement_42}Rebuild {SETTLEMENT}");
            title.SetTextVariable("SETTLEMENT", target.Name);

            InformationManager.ShowTextInquiry(new TextInquiryData(title.ToString(), new TextObject("{=player_settlement_03}What would you like to name your settlement?").ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                (string settlementName) =>
                {
                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;

                    if (string.IsNullOrEmpty(settlementName))
                    {
                        settlementName = target.Name.ToString();
                    }

                    void Apply(string settlementName, CultureObject culture, string? villageType)
                    {
                        settlementPlacementFrame = null;

                        void ApplyPlaced(string settlementName, CultureObject culture, string? villageType)
                        {
                            Settlement? bound = target.Village?.Bound;

                            Settlement settlement;

                            if (currentModelOptionIdx < 0)
                            {
                                currentModelOptionIdx = new Random().Next(0, availableModels!.Count);
                            }

                            var atPos = settlementPlacementFrame?.origin != null ? settlementPlacementFrame.Value.origin.AsVec2 : MobileParty.MainParty.GetPosition2D;

                            var gPos = atPos;

                            var item = availableModels![currentModelOptionIdx];

                            var node = item.ItemXML.CloneNode(true);

                            var newId = target.StringId;

                            var oldCompId = target.SettlementComponent.StringId;

                            node.Attributes["id"].Value = newId;
                            node.Attributes["posX"].Value = atPos.X.ToString();
                            node.Attributes["posY"].Value = atPos.Y.ToString();
                            node.Attributes["name"].Value = settlementName;
                            node.Attributes["culture"].Value = $"Culture.{culture.StringId}";

                            if (node.Attributes["owner"] != null)
                            {
                                node.Attributes["owner"].Value = $"Faction.{Hero.MainHero.Clan.StringId}";
                            }

                            var newNodeComponent = node.SelectSingleNode(settlementType == SettlementType.Village ? "descendant::Village" : "descendant::Town");
                            newNodeComponent.Attributes["id"].Value = oldCompId;

                            if (settlementType == SettlementType.Village)
                            {
                                newNodeComponent.Attributes["village_type"].Value = $"VillageType.{villageType}";
                                newNodeComponent.Attributes["bound"].Value = $"Settlement.{bound?.StringId}";
                            }
                            else
                            {

                                // If a gate position has been placed, use that instead.
                                if (gateSupported && Main.Settings!.AllowGatePosition && gatePlacementFrame != null)
                                {
                                    gPos = gatePlacementFrame?.origin != null ? gatePlacementFrame.Value.origin.AsVec2 : gPos;

                                    //gate_posX = "{{G_POS_X}}"
                                    if (node.Attributes["gate_posX"] == null)
                                    {
                                        XmlAttribute gatePosXAttribute = node.OwnerDocument.CreateAttribute("gate_posX");
                                        gatePosXAttribute.Value = gPos.X.ToString();
                                        node.Attributes.SetNamedItem(gatePosXAttribute);
                                    }
                                    else
                                    {
                                        node.Attributes["gate_posX"].Value = gPos.X.ToString();
                                    }
                                    //gate_posY = "{{G_POS_Y}}"
                                    if (node.Attributes["gate_posY"] == null)
                                    {
                                        XmlAttribute gatePosYAttribute = node.OwnerDocument.CreateAttribute("gate_posY");
                                        gatePosYAttribute.Value = gPos.Y.ToString();
                                        node.Attributes.SetNamedItem(gatePosYAttribute);
                                    }
                                    else
                                    {
                                        node.Attributes["gate_posY"].Value = gPos.Y.ToString();
                                    }
                                }
                            }

                            TextObject encyclopediaText = target.EncyclopediaText;

                            if (node.Attributes["text"] == null)
                            {
                                XmlAttribute encyclopediaTextAttribute = node.OwnerDocument.CreateAttribute("text");
                                encyclopediaTextAttribute.Value = encyclopediaText.ToString();
                                node.Attributes.SetNamedItem(encyclopediaTextAttribute);
                            }
                            else
                            {
                                node.Attributes["text"].Value = encyclopediaText.ToString();
                            }

                            var xml = $"<Settlements>{node.OuterXml}</Settlements>";
                            xml = xml.Replace("{{G_POS_X}}", (gPos.X).ToString());
                            xml = xml.Replace("{{G_POS_Y}}", (gPos.Y).ToString());

                            target.IsOverwritten(out OverwriteSettlementItem? overwriteItem);

                            overwriteItem ??= new OverwriteSettlementItem();

                            overwriteItem.ItemXML = xml;
                            overwriteItem.Type = (int) SettlementType.Town;
                            overwriteItem.SettlementName = settlementName;
                            overwriteItem.RotationMat3 = settlementPlacementFrame?.rotation;
                            overwriteItem.DeepEdits = new List<DeepTransformEdit>(deepEditPrefab == settlementVisualPrefab && deepTransformEdits != null ? deepTransformEdits : new());
                            overwriteItem.Version = Main.Version;
                            overwriteItem.StringId = newId;
                            overwriteItem.PrefabId = item.Id;

                            if (PlayerSettlementInfo.Instance!.OverwriteSettlements == null)
                            {
                                PlayerSettlementInfo.Instance.OverwriteSettlements = new();
                            }

                            PlayerSettlementInfo.Instance!.OverwriteSettlements.Add(overwriteItem);

                            var doc = new XmlDocument();
                            doc.LoadXml(xml);
                            MBObjectManager.Instance.LoadXml(doc);

                            settlement = MBObjectManager.Instance.GetObject<Settlement>(overwriteItem.StringId);
                            overwriteItem.Settlement = settlement;

                            if (settlementType == SettlementType.Village && bound != null)
                            {
                                settlement.SetBound(bound);
                            }

                            settlement.SetName(new TextObject(settlementName));

                            settlement.Party.SetLevelMaskIsDirty();
                            settlement.IsVisible = true;
                            settlement.IsInspected = true;
                            settlement.Party.SetVisualAsDirty();

                            overwriteItem.BuiltAt = Campaign.CurrentTime;
                            overwriteItem.BuildComplete = false;


                            // Rebuild cost when enabled
                            switch (settlementType)
                            {
                                case SettlementType.None:
                                    break;
                                case SettlementType.Town:
                                    {
                                        if (Main.Settings!.RequireGold)
                                        {
                                            GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, settlement, Main.Settings.RebuildTownRequiredGold, true);
                                        }
                                    }
                                    break;
                                case SettlementType.Village:
                                    {
                                        if (Main.Settings!.RequireVillageGold)
                                        {
                                            GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, settlement, Main.Settings.RebuildVillageRequiredGold, true);
                                        }
                                    }
                                    break;
                                case SettlementType.Castle:
                                    {
                                        if (Main.Settings!.RequireCastleGold)
                                        {
                                            GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, settlement, Main.Settings.RebuildCastleRequiredGold, true);
                                        }
                                    }
                                    break;
                            }

                            OnResetEvent?.ClearListeners(target);
                            SaveHandler.SaveLoad(!Main.Settings!.CreateNewSave);
                        }


                        void ConfirmAndApply()
                        {
                            var createPlayerSettlementText = settlementType == SettlementType.Village ? new TextObject("{=player_settlement_13}Build a Village").ToString() : settlementType == SettlementType.Castle ? new TextObject("{=player_settlement_19}Build a Castle").ToString() : new TextObject("{=player_settlement_04}Build a Town").ToString();
                            var confirm = settlementType == SettlementType.Village ? new TextObject("{=player_settlement_14}Are you sure you want to build your village here?") : settlementType == SettlementType.Castle ? new TextObject("{=player_settlement_18}Are you sure you want to build your castle here?") : new TextObject("{=player_settlement_05}Are you sure you want to build your town here?");

                            InformationManager.ShowInquiry(new InquiryData(createPlayerSettlementText, confirm.ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                                () =>
                                {
                                    InformationManager.HideInquiry();
                                    ApplyPlaced(settlementName, culture, villageType);
                                },
                                () =>
                                {
                                    // Cancelled. Do nothing.
                                    InformationManager.HideInquiry();

                                    // If not in placement, we have to reset completely. Otherwise we can just return to placement
                                    if (!Main.Settings!.SettlementPlacement)
                                    {
                                        Reset();
                                        MapBarExtensionVM.Current?.OnRefresh();
                                    }
                                }), true, false);
                        }

                        availableModels?.Clear();
                        switch (settlementType)
                        {
                            case SettlementType.Village:
                                {
                                    if (Main.Settings!.SelectedCultureOnly && Main.Submodule!.CultureTemplates.ContainsKey(culture.StringId))
                                    {
                                        availableModels = Main.Submodule.CultureTemplates[culture.StringId].SelectMany((cst) => SelectVillageTemplates(cst, target.Village!.Bound.IsCastle)).ToList();
                                        currentModelOptionIdx = -1;
                                    }

                                    if (availableModels == null || availableModels.Count == 0)
                                    {
                                        availableModels = Main.Submodule!.CultureTemplates.Values.SelectMany(c => c.SelectMany((cst) => SelectVillageTemplates(cst, target.Village!.Bound.IsCastle))).ToList();
                                        currentModelOptionIdx = availableModels.FindIndex(a => a.Culture == culture.StringId) - 1;
                                    }

                                }
                                break;

                            case SettlementType.Castle:
                                {
                                    if (Main.Settings!.SelectedCultureOnly && Main.Submodule!.CultureTemplates.ContainsKey(culture.StringId))
                                    {
                                        availableModels = Main.Submodule.CultureTemplates[culture.StringId].SelectMany(SelectCastleTemplates).ToList();
                                        currentModelOptionIdx = -1;
                                    }

                                    if (availableModels == null || availableModels.Count == 0)
                                    {
                                        availableModels = Main.Submodule!.CultureTemplates.Values.SelectMany(c => c.SelectMany(SelectCastleTemplates)).ToList();
                                        currentModelOptionIdx = availableModels.FindIndex(a => a.Culture == culture.StringId) - 1;
                                    }
                                }
                                break;
                            case SettlementType.Town:
                                {
                                    if (Main.Settings!.SelectedCultureOnly && Main.Submodule!.CultureTemplates.ContainsKey(culture.StringId))
                                    {
                                        availableModels = Main.Submodule.CultureTemplates[culture.StringId].SelectMany(SelectTownTemplates).ToList();
                                        currentModelOptionIdx = -1;
                                    }

                                    if (availableModels == null || availableModels.Count == 0)
                                    {
                                        availableModels = Main.Submodule!.CultureTemplates.Values.SelectMany(c => c.SelectMany(SelectTownTemplates)).ToList();
                                        currentModelOptionIdx = availableModels.FindIndex(a => a.Culture == culture.StringId) - 1;
                                    }
                                }
                                break;
                            default:
                                // Cancelled. Do nothing.
                                InformationManager.HideInquiry();

                                // If not in placement, we have to reset completely. Otherwise we can just return to placement
                                if (!Main.Settings!.SettlementPlacement)
                                {
                                    Reset();
                                    MapBarExtensionVM.Current?.OnRefresh();
                                }
                                return;
                        }

                        if (!Main.Settings!.SettlementPlacement)
                        {
                            ConfirmAndApply();
                            return;
                        }

                        StartSettlementPlacement();

                        applyPending = () => ConfirmAndApply();
                    }

                    if (Main.Settings!.ForcePlayerCulture)
                    {
                        if (Main.Settings.AutoAllocateVillageType || settlementType != SettlementType.Village)
                        {
                            Apply(settlementName, Hero.MainHero.Culture, settlementType == SettlementType.Village ? AutoCalculateVillageType(-1) : null);
                        }
                        else
                        {
                            DetermineVillageType(settlementName, Hero.MainHero.Culture, target.Village?.Bound, -1, Apply);
                        }
                        return;
                    }

                    var titleText = settlementType == SettlementType.Castle ? new TextObject("{=player_settlement_20}Choose castle culture") :
                                    settlementType == SettlementType.Village ? new TextObject("{=player_settlement_11}Choose village culture")
                                                                             : new TextObject("{=player_settlement_09}Choose town culture");
                    var descriptionText = settlementType == SettlementType.Castle ? new TextObject("{=player_settlement_21}Choose the culture for {CASTLE}") :
                                          settlementType == SettlementType.Village ? new TextObject("{=player_settlement_12}Choose the culture for {VILLAGE}")
                                                                                     : new TextObject("{=player_settlement_10}Choose the culture for {TOWN}");
                    descriptionText.SetTextVariable("CASTLE", settlementName);
                    descriptionText.SetTextVariable("TOWN", settlementName);
                    descriptionText.SetTextVariable("VILLAGE", settlementName);

                    List<InquiryElement> inquiryElements1 = GetCultures(true).Select(c => new InquiryElement(c, c.Name.ToString(), new BannerImageIdentifier(new Banner(c.Banner)), true, c.Name.ToString())).ToList();

                    MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                        titleText: titleText.ToString(),
                        descriptionText: descriptionText.ToString(),
                        inquiryElements: inquiryElements1,
                        isExitShown: false,
                        maxSelectableOptionCount: 1,
                        minSelectableOptionCount: 1,
                        affirmativeText: GameTexts.FindText("str_ok", null).ToString(),
                        negativeText: null,
                        affirmativeAction: (List<InquiryElement> args) =>
                        {
                            List<InquiryElement> source = args;

                            CultureObject culture = (args?.FirstOrDefault()?.Identifier as CultureObject) ?? Hero.MainHero.Culture;

                            if (Main.Settings.AutoAllocateVillageType || settlementType != SettlementType.Village)
                            {
                                Apply(settlementName, culture, AutoCalculateVillageType(-1));
                            }
                            else
                            {
                                DetermineVillageType(settlementName, culture, target.Village?.Bound, -1, Apply);
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
                    Reset();
                    MapBarExtensionVM.Current?.OnRefresh();
                }, false, new Func<string, Tuple<bool, string>>(FactionHelper.IsKingdomNameApplicable), "", target.Name.ToString()), true, false);
        }

        private void Rebuild(PlayerSettlementItem target)
        {
            SettlementType settlementType = target.GetSettlementType();
            gateSupported = settlementType != SettlementType.Village;

            InformationManager.ShowTextInquiry(new TextInquiryData(new TextObject("{=player_settlement_39}Rebuild Player Settlement").ToString(), new TextObject("{=player_settlement_03}What would you like to name your settlement?").ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                (string settlementName) =>
                {
                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;

                    if (string.IsNullOrEmpty(settlementName))
                    {
                        settlementName = target.SettlementName;
                    }

                    void Apply(string settlementName, CultureObject culture, string? villageType)
                    {
                        settlementPlacementFrame = null;

                        void ApplyPlaced(string settlementName, CultureObject culture, string villageType)
                        {
                            Settlement? bound = target.Settlement?.Village?.Bound;

                            Settlement settlement;

                            if (currentModelOptionIdx < 0)
                            {
                                currentModelOptionIdx = new Random().Next(0, availableModels.Count);
                            }

                            var atPos = settlementPlacementFrame?.origin != null ? settlementPlacementFrame.Value.origin.AsVec2 : MobileParty.MainParty.GetPosition2D;

                            var gPos = atPos;

                            var item = availableModels[currentModelOptionIdx];


                            var docX = new XmlDocument();
                            docX.LoadXml(target.ItemXML);
                            var oldNode = docX.ChildNodes[0].ChildNodes.OfType<XmlNode>().FirstOrDefault(n => n is not XmlComment);

                            var node = item.ItemXML.CloneNode(true);

                            var newId = oldNode.Attributes["id"].Value;
                            var oldNodeComponent = oldNode.SelectSingleNode(settlementType == SettlementType.Village ? "descendant::Village" : "descendant::Town");

                            var oldCompId = oldNodeComponent.Attributes["id"].Value;

                            node.Attributes["id"].Value = newId;
                            node.Attributes["posX"].Value = atPos.X.ToString();
                            node.Attributes["posY"].Value = atPos.Y.ToString();
                            node.Attributes["name"].Value = settlementName;
                            node.Attributes["culture"].Value = $"Culture.{culture.StringId}";

                            if (node.Attributes["owner"] != null)
                            {
                                node.Attributes["owner"].Value = $"Faction.{Hero.MainHero.Clan.StringId}";
                            }

                            var newNodeComponent = node.SelectSingleNode(settlementType == SettlementType.Village ? "descendant::Village" : "descendant::Town");
                            newNodeComponent.Attributes["id"].Value = oldCompId;

                            if (settlementType == SettlementType.Village)
                            {
                                newNodeComponent.Attributes["village_type"].Value = $"VillageType.{villageType}";
                                newNodeComponent.Attributes["bound"].Value = $"Settlement.{bound?.StringId}";
                            }
                            else
                            {

                                // If a gate position has been placed, use that instead.
                                if (gateSupported && Main.Settings!.AllowGatePosition && gatePlacementFrame != null)
                                {
                                    gPos = gatePlacementFrame?.origin != null ? gatePlacementFrame.Value.origin.AsVec2 : gPos;

                                    //gate_posX = "{{G_POS_X}}"
                                    if (node.Attributes["gate_posX"] == null)
                                    {
                                        XmlAttribute gatePosXAttribute = node.OwnerDocument.CreateAttribute("gate_posX");
                                        gatePosXAttribute.Value = gPos.X.ToString();
                                        node.Attributes.SetNamedItem(gatePosXAttribute);
                                    }
                                    else
                                    {
                                        node.Attributes["gate_posX"].Value = gPos.X.ToString();
                                    }
                                    //gate_posY = "{{G_POS_Y}}"
                                    if (node.Attributes["gate_posY"] == null)
                                    {
                                        XmlAttribute gatePosYAttribute = node.OwnerDocument.CreateAttribute("gate_posY");
                                        gatePosYAttribute.Value = gPos.Y.ToString();
                                        node.Attributes.SetNamedItem(gatePosYAttribute);
                                    }
                                    else
                                    {
                                        node.Attributes["gate_posY"].Value = gPos.Y.ToString();
                                    }
                                }
                            }

                            TextObject encyclopediaText = new TextObject(oldNode.Attributes["text"] != null ? oldNode.Attributes["text"].Value : "");

                            if (node.Attributes["text"] == null)
                            {
                                XmlAttribute encyclopediaTextAttribute = node.OwnerDocument.CreateAttribute("text");
                                encyclopediaTextAttribute.Value = encyclopediaText.ToString();
                                node.Attributes.SetNamedItem(encyclopediaTextAttribute);
                            }
                            else
                            {
                                node.Attributes["text"].Value = encyclopediaText.ToString();
                            }

                            var xml = $"<Settlements>{node.OuterXml}</Settlements>";
                            xml = xml.Replace("{{G_POS_X}}", (gPos.X).ToString());
                            xml = xml.Replace("{{G_POS_Y}}", (gPos.Y).ToString());

                            target.ItemXML = xml;
                            target.SettlementName = settlementName;
                            target.RotationMat3 = settlementPlacementFrame?.rotation;
                            target.DeepEdits = new List<DeepTransformEdit>(deepEditPrefab == settlementVisualPrefab && deepTransformEdits != null ? deepTransformEdits : new());
                            target.Version = Main.Version;
                            target.PrefabId = item.Id;
                            target.StringId = newId;

                            var doc = new XmlDocument();
                            doc.LoadXml(xml);
                            MBObjectManager.Instance.LoadXml(doc);

                            settlement = MBObjectManager.Instance.GetObject<Settlement>(target.StringId);
                            target.Settlement = settlement;

                            if (settlementType == SettlementType.Village && bound != null)
                            {
                                settlement.SetBound(bound);
                            }

                            settlement.SetName(new TextObject(settlementName));

                            settlement.Party.SetLevelMaskIsDirty();
                            settlement.IsVisible = true;
                            settlement.IsInspected = true;
                            settlement.Party.SetVisualAsDirty();

                            // Rebuild applies if the build was already complete
                            target.IsRebuild = true && target.BuildComplete;
                            target.BuiltAt = Campaign.CurrentTime;
                            target.BuildComplete = false;


                            // Rebuild cost when enabled
                            switch (settlementType)
                            {
                                case SettlementType.None:
                                    break;
                                case SettlementType.Town:
                                    {
                                        if (Main.Settings!.RequireGold)
                                        {
                                            GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, settlement, Main.Settings.RebuildTownRequiredGold, true);
                                        }
                                    }
                                    break;
                                case SettlementType.Village:
                                    {
                                        if (Main.Settings!.RequireVillageGold)
                                        {
                                            GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, settlement, Main.Settings.RebuildVillageRequiredGold, true);
                                        }
                                    }
                                    break;
                                case SettlementType.Castle:
                                    {
                                        if (Main.Settings!.RequireCastleGold)
                                        {
                                            GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, settlement, Main.Settings.RebuildCastleRequiredGold, true);
                                        }
                                    }
                                    break;
                            }

                            OnResetEvent?.ClearListeners(target);
                            SaveHandler.SaveLoad(!Main.Settings!.CreateNewSave);
                        }


                        void ConfirmAndApply()
                        {
                            var createPlayerSettlementText = settlementType == SettlementType.Village ? new TextObject("{=player_settlement_13}Build a Village").ToString() : settlementType == SettlementType.Castle ? new TextObject("{=player_settlement_19}Build a Castle").ToString() : new TextObject("{=player_settlement_04}Build a Town").ToString();
                            var confirm = settlementType == SettlementType.Village ? new TextObject("{=player_settlement_14}Are you sure you want to build your village here?") : settlementType == SettlementType.Castle ? new TextObject("{=player_settlement_18}Are you sure you want to build your castle here?") : new TextObject("{=player_settlement_05}Are you sure you want to build your town here?");

                            InformationManager.ShowInquiry(new InquiryData(createPlayerSettlementText, confirm.ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                                () =>
                                {
                                    InformationManager.HideInquiry();
                                    ApplyPlaced(settlementName, culture, villageType);
                                },
                                () =>
                                {
                                    // Cancelled. Do nothing.
                                    InformationManager.HideInquiry();

                                    // If not in placement, we have to reset completely. Otherwise we can just return to placement
                                    if (!Main.Settings!.SettlementPlacement)
                                    {
                                        Reset();
                                        MapBarExtensionVM.Current?.OnRefresh();
                                    }
                                }), true, false);
                        }

                        availableModels?.Clear();
                        switch (settlementType)
                        {
                            case SettlementType.Village:
                                {
                                    if (Main.Settings!.SelectedCultureOnly && Main.Submodule!.CultureTemplates.ContainsKey(culture.StringId))
                                    {
                                        availableModels = Main.Submodule.CultureTemplates[culture.StringId].SelectMany((cst) => SelectVillageTemplates(cst, target.Settlement!.Village!.Bound.IsCastle)).ToList();
                                        currentModelOptionIdx = -1;
                                    }

                                    if (availableModels == null || availableModels.Count == 0)
                                    {
                                        availableModels = Main.Submodule!.CultureTemplates.Values.SelectMany(c => c.SelectMany((cst) => SelectVillageTemplates(cst, target.Settlement!.Village!.Bound.IsCastle))).ToList();
                                        currentModelOptionIdx = availableModels.FindIndex(a => a.Culture == culture.StringId) - 1;
                                    }

                                }
                                break;

                            case SettlementType.Castle:
                                {
                                    if (Main.Settings!.SelectedCultureOnly && Main.Submodule!.CultureTemplates.ContainsKey(culture.StringId))
                                    {
                                        availableModels = Main.Submodule.CultureTemplates[culture.StringId].SelectMany(SelectCastleTemplates).ToList();
                                        currentModelOptionIdx = -1;
                                    }

                                    if (availableModels == null || availableModels.Count == 0)
                                    {
                                        availableModels = Main.Submodule!.CultureTemplates.Values.SelectMany(c => c.SelectMany(SelectCastleTemplates)).ToList();
                                        currentModelOptionIdx = availableModels.FindIndex(a => a.Culture == culture.StringId) - 1;
                                    }
                                }
                                break;
                            case SettlementType.Town:
                                {
                                    if (Main.Settings!.SelectedCultureOnly && Main.Submodule!.CultureTemplates.ContainsKey(culture.StringId))
                                    {
                                        availableModels = Main.Submodule.CultureTemplates[culture.StringId].SelectMany(SelectTownTemplates).ToList();
                                        currentModelOptionIdx = -1;
                                    }

                                    if (availableModels == null || availableModels.Count == 0)
                                    {
                                        availableModels = Main.Submodule!.CultureTemplates.Values.SelectMany(c => c.SelectMany(SelectTownTemplates)).ToList();
                                        currentModelOptionIdx = availableModels.FindIndex(a => a.Culture == culture.StringId) - 1;
                                    }
                                }
                                break;
                            default:
                                // Cancelled. Do nothing.
                                InformationManager.HideInquiry();

                                // If not in placement, we have to reset completely. Otherwise we can just return to placement
                                if (!Main.Settings!.SettlementPlacement)
                                {
                                    Reset();
                                    MapBarExtensionVM.Current?.OnRefresh();
                                }
                                return;
                        }

                        var curModelPrefab = (!string.IsNullOrEmpty(target.PrefabId) ? target.PrefabId : !string.IsNullOrEmpty(target.StringId) ? target.StringId : target.Settlement!.StringId);
                        var curModelIdx = availableModels?.FindIndex(a => a.Id == curModelPrefab) ?? -1;

                        // If the current model is still applicable with the new culture selection, keep the current model as starting point. Additionally keep any deep edits that apply.
                        if (curModelIdx >= 0)
                        {
                            // Move index back by 1 since start placement moves one forward again
                            currentModelOptionIdx = curModelIdx - 1;
                            settlementVisualPrefab = deepEditPrefab = curModelPrefab;
                            deepTransformEdits = new List<DeepTransformEdit>(target.DeepEdits ?? new List<DeepTransformEdit>());
                        }

                        if (!Main.Settings!.SettlementPlacement)
                        {
                            ConfirmAndApply();
                            return;
                        }

                        StartSettlementPlacement();

                        applyPending = () => ConfirmAndApply();
                    }



                    if (Main.Settings!.ForcePlayerCulture)
                    {
                        if (Main.Settings.AutoAllocateVillageType || settlementType != SettlementType.Village)
                        {
                            Apply(settlementName, Hero.MainHero.Culture, settlementType == SettlementType.Village ? AutoCalculateVillageType(target.Identifier) : null);
                        }
                        else
                        {
                            DetermineVillageType(settlementName, Hero.MainHero.Culture, target.Settlement?.Village?.Bound, target.Identifier, Apply);
                        }
                        return;
                    }

                    var titleText = settlementType == SettlementType.Castle ? new TextObject("{=player_settlement_20}Choose castle culture") :
                                    settlementType == SettlementType.Village ? new TextObject("{=player_settlement_11}Choose village culture")
                                                                             : new TextObject("{=player_settlement_09}Choose town culture");
                    var descriptionText = settlementType == SettlementType.Castle ? new TextObject("{=player_settlement_21}Choose the culture for {CASTLE}") :
                                          settlementType == SettlementType.Village ? new TextObject("{=player_settlement_12}Choose the culture for {VILLAGE}")
                                                                                     : new TextObject("{=player_settlement_10}Choose the culture for {TOWN}");
                    descriptionText.SetTextVariable("CASTLE", settlementName);
                    descriptionText.SetTextVariable("TOWN", settlementName);
                    descriptionText.SetTextVariable("VILLAGE", settlementName);

                    List<InquiryElement> inquiryElements1 = GetCultures(true).Select(c => new InquiryElement(c, c.Name.ToString(), new BannerImageIdentifier(new Banner(c.Banner)), true, c.Name.ToString())).ToList();

                    MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                        titleText: titleText.ToString(),
                        descriptionText: descriptionText.ToString(),
                        inquiryElements: inquiryElements1,
                        isExitShown: false,
                        maxSelectableOptionCount: 1,
                        minSelectableOptionCount: 1,
                        affirmativeText: GameTexts.FindText("str_ok", null).ToString(),
                        negativeText: null,
                        affirmativeAction: (List<InquiryElement> args) =>
                        {
                            List<InquiryElement> source = args;

                            CultureObject culture = (args?.FirstOrDefault()?.Identifier as CultureObject) ?? Hero.MainHero.Culture;

                            if (Main.Settings.AutoAllocateVillageType || settlementType != SettlementType.Village)
                            {
                                Apply(settlementName, culture, AutoCalculateVillageType(target.Identifier));
                            }
                            else
                            {
                                DetermineVillageType(settlementName, culture, target.Settlement?.Village?.Bound, target.Identifier, Apply);
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
                    Reset();
                    MapBarExtensionVM.Current?.OnRefresh();
                }, false, new Func<string, Tuple<bool, string>>(FactionHelper.IsKingdomNameApplicable), "", target.SettlementName), true, false);
        }

        private void BuildCastle()
        {
            gateSupported = true;
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;

            InformationManager.ShowTextInquiry(new TextInquiryData(new TextObject("{=player_settlement_02}Create Player Settlement").ToString(), new TextObject("{=player_settlement_03}What would you like to name your settlement?").ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                (string settlementName) =>
                {
                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;

                    if (string.IsNullOrEmpty(settlementName))
                    {
                        settlementName = new TextObject("{=player_settlement_n_01}Player Settlement").ToString();
                    }

                    void ApplyPlaced(string settlementName, CultureObject culture)
                    {
                        var castleSettlement = CreateCastle(settlementName, culture, out PlayerSettlementItem castleItem);

                        castleSettlement.Town.OwnerClan = Hero.MainHero.Clan;

                        castleSettlement.SetName(new TextObject(settlementName));

                        castleSettlement.Party.SetLevelMaskIsDirty();
                        castleSettlement.IsVisible = true;
                        castleSettlement.IsInspected = true;
                        castleSettlement.Town.FoodStocks = (float) castleSettlement.Town.FoodStocksUpperLimit();
                        castleSettlement.Party.SetVisualAsDirty();

                        SettlementVisualManager.Current.AddNewPartyVisualForParty(castleSettlement.Party);

                        castleSettlement.OnGameCreated();
                        castleSettlement.AfterInitialized();
                        castleSettlement.OnFinishLoadState();

                        var castle = castleSettlement.Town;

                        InitCastleBuildings(castleSettlement);

                        //var campaignGameStarter = SandBoxManager.Instance.GameStarter;
                        //var craftingCampaignBehavior = campaignGameStarter.CampaignBehaviors.FirstOrDefault(b => b is CraftingCampaignBehavior) as CraftingCampaignBehavior;
                        //craftingCampaignBehavior?.AddTown(castle, out _);

                        castleItem.BuiltAt = Campaign.CurrentTime;

                        if (Main.Settings!.RequireCastleGold)
                        {
                            GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, castleSettlement, Main.Settings.RequiredCastleGold, true);
                        }
                        else
                        {
                            castleSettlement.SettlementComponent.ChangeGold(3_000);
                        }

                        if (Main.Settings.AddInitialGarrison)
                        {

                            castleSettlement.AddGarrisonParty();
                            var garrisonTroopsCampaignBehavior = Campaign.Current.GetCampaignBehavior<GarrisonTroopsCampaignBehavior>();
                            if (garrisonTroopsCampaignBehavior != null && castleSettlement.Town != null)
                            {
                                FillGarrisonPartyOnNewGameInvoker.Invoke(garrisonTroopsCampaignBehavior, new object[] { castleSettlement.Town });
                            }
                            castleSettlement.SetGarrisonWagePaymentLimit(Campaign.Current.Models.PartyWageModel.MaxWagePaymentLimit);
                        }

                        if (Main.Settings.AddInitialMilitia)
                        {
                            castleSettlement.Militia = castleSettlement.Town.MilitiaChange * 45f;
                        }

                        var campaignGameStarter = SandBoxManager.Instance.GameStarter;
                        var rc = campaignGameStarter.CampaignBehaviors.FirstOrDefault(b => b is RecruitmentCampaignBehavior) as RecruitmentCampaignBehavior;
                        if (rc is RecruitmentCampaignBehavior recruitmentCampaignBehavior)
                        {
                            recruitmentCampaignBehavior.NewSettlementBuilt(castleSettlement);
                        }

                        _settlementCreated.Invoke(castleSettlement);
                        SaveHandler.SaveLoad(!Main.Settings.CreateNewSave);
                    }

                    void Apply(string settlementName, CultureObject culture)
                    {
                        settlementPlacementFrame = null;

                        void ConfirmAndApply()
                        {
                            var createPlayerSettlementText = new TextObject("{=player_settlement_19}Build a Castle").ToString();
                            var confirm = new TextObject("{=player_settlement_18}Are you sure you want to build your castle here?");

                            InformationManager.ShowInquiry(new InquiryData(createPlayerSettlementText, confirm.ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                                () =>
                                {
                                    InformationManager.HideInquiry();
                                    ApplyPlaced(settlementName, culture);
                                },
                                () =>
                                {
                                    // Cancelled. Do nothing.
                                    InformationManager.HideInquiry();

                                    // If not in placement, we have to reset completely. Otherwise we can just return to placement
                                    if (!Main.Settings!.SettlementPlacement)
                                    {
                                        Reset();
                                        MapBarExtensionVM.Current?.OnRefresh();
                                    }
                                }), true, false);
                        }

                        availableModels?.Clear();
                        if (Main.Settings!.SelectedCultureOnly && Main.Submodule!.CultureTemplates.ContainsKey(culture.StringId))
                        {
                            availableModels = Main.Submodule.CultureTemplates[culture.StringId].SelectMany(SelectCastleTemplates).ToList();
                            currentModelOptionIdx = -1;
                        }

                        if (availableModels == null || availableModels.Count == 0)
                        {
                            availableModels = Main.Submodule!.CultureTemplates.Values.SelectMany(c => c.SelectMany(SelectCastleTemplates)).ToList();
                            currentModelOptionIdx = availableModels.FindIndex(a => a.Culture == culture.StringId) - 1;
                        }

                        if (!Main.Settings!.SettlementPlacement)
                        {
                            ConfirmAndApply();
                            return;
                        }

                        StartSettlementPlacement();

                        applyPending = () => ConfirmAndApply();
                    }

                    if (Main.Settings!.ForcePlayerCulture)
                    {
                        Apply(settlementName, Hero.MainHero.Culture);
                        return;
                    }

                    var titleText = new TextObject("{=player_settlement_20}Choose castle culture");
                    var descriptionText = new TextObject("{=player_settlement_21}Choose the culture for {CASTLE}");
                    descriptionText.SetTextVariable("CASTLE", settlementName);


                    List<InquiryElement> inquiryElements1 = GetCultures(true).Select(c => new InquiryElement(c, c.Name.ToString(), new BannerImageIdentifier(new Banner(c.Banner)), true, c.Name.ToString())).ToList();

                    MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                        titleText: titleText.ToString(),
                        descriptionText: descriptionText.ToString(),
                        inquiryElements: inquiryElements1,
                        isExitShown: false,
                        maxSelectableOptionCount: 1,
                        minSelectableOptionCount: 1,
                        affirmativeText: GameTexts.FindText("str_ok", null).ToString(),
                        negativeText: null,
                        affirmativeAction: (List<InquiryElement> args) =>
                        {
                            List<InquiryElement> source = args;
                            //InformationManager.HideInquiry();

                            CultureObject culture = (args?.FirstOrDefault()?.Identifier as CultureObject) ?? Hero.MainHero.Culture;

                            Apply(settlementName, culture);
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
                    Reset();
                    MapBarExtensionVM.Current?.OnRefresh();
                }, false, new Func<string, Tuple<bool, string>>(FactionHelper.IsKingdomNameApplicable), "", ""), true, false);
        }

        List<PlayerSettlementItemTemplate> SelectCastleTemplates(CultureSettlementTemplate cst)
        {
            var templates = new List<PlayerSettlementItemTemplate>();
            try
            {
                var nodes = cst.Document.SelectNodes($"descendant::Settlement[@template_type='Castle']").OfType<XmlNode>();

                foreach (var node in nodes)
                {
                    var id = node.Attributes["id"].Value;

                    if (Main.BlacklistedTemplates.Contains(id))
                    {
                        LogManager.EventTracer.Trace($"Skipped blacklisted template: {id}");
                        continue;
                    }

                    templates.Add(new PlayerSettlementItemTemplate
                    {
                        Id = id,
                        ItemXML = node,
                        Type = (int) SettlementType.Castle,
                        Culture = cst.CultureId
                    });
                }
            }
            catch (Exception e)
            {
                LogManager.Log.NotifyBad(e);
            }
            return templates;
        }

        private static void InitCastleBuildings(Settlement castleSettlement)
        {
            Town castle = castleSettlement.Town;


            // create random number for building levels
            // TODO: Config default building level?
            int num1 = MBRandom.RandomInt(1, 4);
            int num2 = 1;
            // add all buildings
            foreach (BuildingType all2 in BuildingType.All)
            {
                // if already added, skip
                if (castle.Buildings.Any<Building>((Building k) => k.BuildingType.StringId == all2.StringId))
                {
                    continue;
                }
                // only castle buildings
                if (!all2.StringId.StartsWith("building_castle"))
                {
                    continue;
                }

                castle.Buildings.Add(new Building(all2, castle, 0f, num1));
            }


            foreach (Building building1 in
                from k in castle.Buildings
                orderby k.CurrentLevel descending
                select k)
            {
                if (building1.CurrentLevel == 3 || building1.CurrentLevel == building1.BuildingType.StartLevel || building1.BuildingType.IsDailyProject)
                {
                    continue;
                }
                castle.BuildingsInProgress.Enqueue(building1);
            }

            Building dailyDefault = castleSettlement.Town.Buildings.FirstOrDefault(b => b.IsCurrentlyDefault);
            if (dailyDefault == null || !dailyDefault.BuildingType.IsDailyProject)
            {
                dailyDefault = castleSettlement.Town.Buildings.FirstOrDefault(b => b.BuildingType.IsDailyProject);
                BuildingHelper.ChangeDefaultBuilding(dailyDefault, castleSettlement.Town);
                dailyDefault.IsCurrentlyDefault = true;
            }


            //if (castleSettlement.Town.BuildingsInProgress.IsEmpty() && castleSettlement.Town.CurrentDefaultBuilding == null)
            //{
            //    BuildingHelper.ChangeDefaultBuilding(castleSettlement.Town.Buildings.FirstOrDefault(), castleSettlement.Town);
            //}
        }

        private Settlement CreateCastle(string settlementName, CultureObject culture, out PlayerSettlementItem castleItem)
        {
            if (currentModelOptionIdx < 0)
            {
                currentModelOptionIdx = new Random().Next(0, availableModels.Count);
            }

            if (PlayerSettlementInfo.Instance!.Castles == null)
            {
                PlayerSettlementInfo.Instance!.Castles = new();
            }

            var atPos = settlementPlacementFrame?.origin != null ? settlementPlacementFrame.Value.origin.AsVec2 : MobileParty.MainParty.GetPosition2D;

            var castleNumber = PlayerSettlementInfo.Instance!.Castles!.Count + 1;

            var gPos = atPos;

            var item = availableModels[currentModelOptionIdx];

            string identifierUniqueness = MBRandom.RandomInt().ToString();
            var newId = item.Id + "_random_" + identifierUniqueness;

            var node = item.ItemXML.CloneNode(true);

            node.Attributes["id"].Value = newId;

            var nodeComponent = node.SelectSingleNode("descendant::Town");
            nodeComponent.Attributes["id"].Value = newId + "_castle_comp";

            // If a gate position has been placed, use that instead.
            if (gateSupported && Main.Settings!.AllowGatePosition && gatePlacementFrame != null)
            {
                gPos = gatePlacementFrame?.origin != null ? gatePlacementFrame.Value.origin.AsVec2 : gPos;

                //gate_posX = "{{G_POS_X}}"
                if (node.Attributes["gate_posX"] == null)
                {
                    XmlAttribute gatePosXAttribute = node.OwnerDocument.CreateAttribute("gate_posX");
                    gatePosXAttribute.Value = gPos.X.ToString();
                    node.Attributes.SetNamedItem(gatePosXAttribute);
                }
                else
                {
                    node.Attributes["gate_posX"].Value = gPos.X.ToString();
                }
                //gate_posY = "{{G_POS_Y}}"
                if (node.Attributes["gate_posY"] == null)
                {
                    XmlAttribute gatePosYAttribute = node.OwnerDocument.CreateAttribute("gate_posY");
                    gatePosYAttribute.Value = gPos.Y.ToString();
                    node.Attributes.SetNamedItem(gatePosYAttribute);
                }
                else
                {
                    node.Attributes["gate_posY"].Value = gPos.Y.ToString();
                }
            }

            node.Attributes["posX"].Value = atPos.X.ToString();
            node.Attributes["posY"].Value = atPos.Y.ToString();
            node.Attributes["name"].Value = settlementName;
            node.Attributes["owner"].Value = $"Faction.{Hero.MainHero.Clan.StringId}";
            node.Attributes["culture"].Value = $"Culture.{culture.StringId}";

            TextObject encyclopediaText = new TextObject("{=player_settlement_24}{SETTLEMENT_NAME} was founded by {HERO_NAME} of the {FACTION_TERM} on {BUILD_TIME}");
            encyclopediaText.SetTextVariable("SETTLEMENT_NAME", PlayerSettlementItem.EncyclopediaLinkWithName(newId, new TextObject(settlementName)));
            encyclopediaText.SetTextVariable("HERO_NAME", Hero.MainHero.EncyclopediaLinkWithName);
            encyclopediaText.SetTextVariable("FACTION_TERM", Hero.MainHero.Clan.EncyclopediaLinkWithName);
            encyclopediaText.SetTextVariable("BUILD_TIME", CampaignTime.Now.ToString());

            if (node.Attributes["text"] == null)
            {
                XmlAttribute encyclopediaTextAttribute = node.OwnerDocument.CreateAttribute("text");
                encyclopediaTextAttribute.Value = encyclopediaText.ToString();
                node.Attributes.SetNamedItem(encyclopediaTextAttribute);
            }
            else
            {
                node.Attributes["text"].Value = encyclopediaText.ToString();
            }

            var xml = $"<Settlements>{node.OuterXml}</Settlements>";
            xml = xml.Replace("{{G_POS_X}}", (gPos.X).ToString());
            xml = xml.Replace("{{G_POS_Y}}", (gPos.Y).ToString());

            castleItem = new PlayerSettlementItem
            {
                ItemXML = xml,
                Identifier = castleNumber,
                Type = (int) SettlementType.Castle,
                SettlementName = settlementName,
                RotationMat3 = settlementPlacementFrame?.rotation,
                DeepEdits = new List<DeepTransformEdit>(deepEditPrefab == settlementVisualPrefab && deepTransformEdits != null ? deepTransformEdits : new()),
                Version = Main.Version,
                StringId = newId,
                PrefabId = item.Id
            };
            PlayerSettlementInfo.Instance.Castles.Add(castleItem);

            var doc = new XmlDocument();
            doc.LoadXml(xml);

            Campaign.Current.AsCampaignGameLoadingType(Campaign.GameLoadingType.NewCampaign, () =>
            {
                MBObjectManager.Instance.LoadXml(doc);
                return true;
            });

            var castleSettlement = MBObjectManager.Instance.GetObject<Settlement>(castleItem.StringId);
            castleItem.Settlement = castleSettlement;

            return castleSettlement;
        }

        private void BuildVillage()
        {
            gateSupported = false;
            if (Main.Settings!.AutoDetermineVillageOwner)
            {
                BuildVillageFor(null);
                return;
            }

            var titleText = new TextObject("{=player_settlement_22}Choose village bound settlement");
            var descriptionText = new TextObject("{=player_settlement_23}Choose the settlement to which this village is bound");

            List<InquiryElement> inquiryElements1 = GetPotentialVillageBoundOwners().Where(s => s != null).Select(c => new InquiryElement(c, c!.Name.ToString(), new CharacterImageIdentifier(CharacterCode.CreateFrom((c.IsTown || c.IsCastle ? c.Town.Governor ?? Hero.MainHero : Hero.MainHero).CharacterObject)), true, (c.EncyclopediaText ?? c.Name).ToString())).ToList();

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                titleText: titleText.ToString(),
                descriptionText: descriptionText.ToString(),
                inquiryElements: inquiryElements1,
                isExitShown: false,
                maxSelectableOptionCount: 1,
                minSelectableOptionCount: 1,
                affirmativeText: GameTexts.FindText("str_ok", null).ToString(),
                negativeText: null,
                affirmativeAction: (List<InquiryElement> args) =>
                {
                    List<InquiryElement> source = args;

                    Settlement? settlement = (args?.FirstOrDefault()?.Identifier as Settlement);

                    BuildVillageFor(settlement);
                },
                negativeAction: (_) =>
                {
                    InformationManager.HideInquiry();
                    Reset();
                    MapBarExtensionVM.Current?.OnRefresh();
                },
                soundEventPath: "")
                ,
                false,
                false);
        }


        private void BuildVillageFor(Settlement? bound)
        {
            PlayerSettlementItem? boundTarget;
            if (bound == null)
            {
                bound = CalculateVillageOwner(/*out boundTarget*/);
            }

            if (bound == null)
            {
                InformationManager.HideInquiry();
                Reset();
                MapBarExtensionVM.Current?.OnRefresh();
                return;
            }

            var villageNumber = PlayerSettlementInfo.Instance!.GetVillageNumber(bound, out boundTarget);

            //if (villageNumber < 1 || boundTarget == null)
            //{
            //    // Not a valid village
            //    return;
            //}

            if (boundTarget != null && boundTarget.Villages == null)
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

                    void ApplyPlaced(string settlementName, CultureObject culture, string villageType)
                    {
                        Settlement villageSettlement = CreateVillage(settlementName, culture, bound, boundTarget, villageType, villageNumber, out PlayerSettlementItem villageItem);

                        villageSettlement.SetBound(bound);

                        villageSettlement.SetName(new TextObject(settlementName));

                        villageSettlement.Party.SetLevelMaskIsDirty();
                        villageSettlement.IsVisible = true;
                        villageSettlement.IsInspected = true;
                        villageSettlement.Party.SetVisualAsDirty();

                        SettlementVisualManager.Current.AddNewPartyVisualForParty(villageSettlement.Party);

                        villageSettlement.OnGameCreated();
                        villageSettlement.AfterInitialized();
                        villageSettlement.OnFinishLoadState();

                        var village = villageSettlement.Village;

                        villageItem.BuiltAt = Campaign.CurrentTime;

                        if (Main.Settings!.RequireVillageGold)
                        {
                            GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, villageSettlement, Main.Settings.RequiredVillageGold, true);
                        }
                        else
                        {
                            village.ChangeGold(3_000);
                        }

                        if (Main.Settings.AddInitialMilitia)
                        {
                            villageSettlement.Militia = villageSettlement.Village.MilitiaChange * 45f;
                        }

                        if (Main.Settings.AddInitialNotables)
                        {
                            int targetNotableCountForSettlement = Campaign.Current.Models.NotableSpawnModel.GetTargetNotableCountForSettlement(villageSettlement, Occupation.RuralNotable);
                            for (int i = 0; i < targetNotableCountForSettlement; i++)
                            {
                                HeroCreator.CreateNotable(Occupation.RuralNotable, villageSettlement);
                            }
                            int num = Campaign.Current.Models.NotableSpawnModel.GetTargetNotableCountForSettlement(villageSettlement, Occupation.Headman);
                            for (int j = 0; j < num; j++)
                            {
                                HeroCreator.CreateNotable(Occupation.Headman, villageSettlement);
                            }

                            PostNotablesAdded(villageSettlement);
                        }

                        float value = 0f;
                        foreach (ValueTuple<ItemObject, float> production in village.VillageType.Productions)
                        {
                            float single = Campaign.Current.Models.VillageProductionCalculatorModel.CalculateDailyProductionAmount(village, production.Item1).ResultNumber;
                            value = value + (float) production.Item1.Value * single;
                        }
                        village.TradeTaxAccumulated = (int) (value * (0.6f + 0.3f * MBRandom.RandomFloat) * Campaign.Current.Models.ClanFinanceModel.RevenueSmoothenFraction());

                        var campaignGameStarter = SandBoxManager.Instance.GameStarter;
                        var b = campaignGameStarter.CampaignBehaviors.FirstOrDefault(b => b is VillageGoodProductionCampaignBehavior) as VillageGoodProductionCampaignBehavior;
                        if (b is VillageGoodProductionCampaignBehavior villageGoodsProduction)
                        {
                            villageGoodsProduction.NewVillageBuilt(village);
                        }

                        var rc = campaignGameStarter.CampaignBehaviors.FirstOrDefault(b => b is RecruitmentCampaignBehavior) as RecruitmentCampaignBehavior;
                        if (rc is RecruitmentCampaignBehavior recruitmentCampaignBehavior)
                        {
                            recruitmentCampaignBehavior.NewSettlementBuilt(villageSettlement);
                        }


                        _settlementCreated.Invoke(villageItem.Settlement!);
                        SaveHandler.SaveLoad(!Main.Settings.CreateNewSave);
                    }

                    void Apply(string settlementName, CultureObject culture, string villageType)
                    {
                        settlementPlacementFrame = null;

                        void ConfirmAndApply()
                        {
                            var createPlayerSettlementText = new TextObject("{=player_settlement_13}Build a Village").ToString();
                            var confirm = new TextObject("{=player_settlement_14}Are you sure you want to build your village here?");

                            InformationManager.ShowInquiry(new InquiryData(createPlayerSettlementText, confirm.ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                                () =>
                                {
                                    InformationManager.HideInquiry();
                                    ApplyPlaced(settlementName, culture, villageType);
                                },
                                () =>
                                {
                                    // Cancelled. Do nothing.
                                    InformationManager.HideInquiry();

                                    // If not in placement, we have to reset completely. Otherwise we can just return to placement
                                    if (!Main.Settings!.SettlementPlacement)
                                    {
                                        Reset();
                                        MapBarExtensionVM.Current?.OnRefresh();
                                    }
                                }), true, false);
                        }

                        availableModels?.Clear();
                        if (Main.Settings!.SelectedCultureOnly && Main.Submodule!.CultureTemplates.ContainsKey(culture.StringId))
                        {
                            availableModels = Main.Submodule.CultureTemplates[culture.StringId].SelectMany((cst) => SelectVillageTemplates(cst, bound.IsCastle)).ToList();
                            currentModelOptionIdx = -1;
                        }

                        if (availableModels == null || availableModels.Count == 0)
                        {
                            availableModels = Main.Submodule!.CultureTemplates.Values.SelectMany(c => c.SelectMany((cst) => SelectVillageTemplates(cst, bound.IsCastle))).ToList();
                            currentModelOptionIdx = availableModels.FindIndex(a => a.Culture == culture.StringId) - 1;
                        }

                        if (!Main.Settings!.SettlementPlacement)
                        {
                            ConfirmAndApply();
                            return;
                        }

                        StartSettlementPlacement();

                        applyPending = () => ConfirmAndApply();
                    }

                    if (Main.Settings!.ForcePlayerCulture)
                    {
                        if (Main.Settings.AutoAllocateVillageType)
                        {
                            Apply(settlementName, Hero.MainHero.Culture, AutoCalculateVillageType(villageNumber));
                        }
                        else
                        {
                            DetermineVillageType(settlementName, Hero.MainHero.Culture, bound, villageNumber, Apply);
                        }
                        return;
                    }

                    var titleText = new TextObject("{=player_settlement_11}Choose village culture");
                    var descriptionText = new TextObject("{=player_settlement_12}Choose the culture for {VILLAGE}");
                    descriptionText.SetTextVariable("VILLAGE", settlementName);


                    List<InquiryElement> inquiryElements1 = GetCultures(true).Select(c => new InquiryElement(c, c.Name.ToString(), new BannerImageIdentifier(new Banner(c.Banner)), true, c.Name.ToString())).ToList();

                    MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                        titleText: titleText.ToString(),
                        descriptionText: descriptionText.ToString(),
                        inquiryElements: inquiryElements1,
                        isExitShown: false,
                        maxSelectableOptionCount: 1,
                        minSelectableOptionCount: 1,
                        affirmativeText: GameTexts.FindText("str_ok", null).ToString(),
                        negativeText: null,
                        affirmativeAction: (List<InquiryElement> args) =>
                        {
                            List<InquiryElement> source = args;

                            CultureObject culture = (args?.FirstOrDefault()?.Identifier as CultureObject) ?? Hero.MainHero.Culture;

                            if (Main.Settings.AutoAllocateVillageType)
                            {
                                //InformationManager.HideInquiry();
                                Apply(settlementName, culture, AutoCalculateVillageType(villageNumber));
                            }
                            else
                            {
                                DetermineVillageType(settlementName, culture, bound, villageNumber, Apply);
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
                    Reset();
                    MapBarExtensionVM.Current?.OnRefresh();
                }, false, new Func<string, Tuple<bool, string>>(FactionHelper.IsKingdomNameApplicable), "", ""), true, false);
        }

        List<PlayerSettlementItemTemplate> SelectVillageTemplates(CultureSettlementTemplate cst, bool forCastle)
        {
            var templates = new List<PlayerSettlementItemTemplate>();
            try
            {
                var nodes = cst.Document.SelectNodes($"descendant::Settlement[@template_type='Village']").OfType<XmlNode>();

                foreach (var node in nodes)
                {
                    var id = node.Attributes["id"].Value.Replace("{{OWNER_TYPE}}", forCastle ? "castle" : "town");

                    if (Main.BlacklistedTemplates.Contains(id))
                    {
                        LogManager.EventTracer.Trace($"Skipped blacklisted template: {id}");
                        continue;
                    }

                    templates.Add(new PlayerSettlementItemTemplate
                    {
                        Id = id,
                        ItemXML = node,
                        Type = (int) SettlementType.Village,
                        Culture = cst.CultureId
                    });
                }
            }
            catch (Exception e)
            {
                LogManager.Log.NotifyBad(e);
            }
            return templates;
        }

        private void DetermineVillageType(string settlementName, CultureObject culture, Settlement? bound, int villageNumber, Action<string, CultureObject, string> Apply)
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
                minSelectableOptionCount: 1,
                affirmativeText: GameTexts.FindText("str_ok", null).ToString(),
                negativeText: null,
                affirmativeAction: (List<InquiryElement> args) =>
                {
                    List<InquiryElement> source = args;
                    //InformationManager.HideInquiry();

                    string villageType = (args?.FirstOrDefault()?.Identifier as VillageType)?.MeshName ?? AutoCalculateVillageType(villageNumber);

                    Apply(settlementName, culture, villageType);
                },
                negativeAction: null,
                soundEventPath: "")
            ,
            false,
            false);
        }

        private Settlement CreateVillage(string settlementName, CultureObject culture, Settlement? bound, PlayerSettlementItem? boundTarget, string villageType, int villageNumber, out PlayerSettlementItem villageItem)
        {

            if (currentModelOptionIdx < 0)
            {
                currentModelOptionIdx = new Random().Next(0, availableModels.Count);
            }

            var atPos = settlementPlacementFrame?.origin != null ? settlementPlacementFrame.Value.origin.AsVec2 : MobileParty.MainParty.GetPosition2D;

            var item = availableModels[currentModelOptionIdx];

            var node = item.ItemXML.CloneNode(true);

            string identifierUniqueness = MBRandom.RandomInt().ToString();
            var newId = item.Id + "_random_" + identifierUniqueness;

            node.Attributes["id"].Value = newId;
            node.Attributes["posX"].Value = atPos.X.ToString();
            node.Attributes["posY"].Value = atPos.Y.ToString();
            node.Attributes["name"].Value = settlementName;
            node.Attributes["culture"].Value = $"Culture.{culture.StringId}";

            var newNodeComponent = node.SelectSingleNode("descendant::Village");
            newNodeComponent.Attributes["id"].Value = newNodeComponent.Attributes["id"].Value.Replace("{{OWNER_TYPE}}", (bound?.IsCastle ?? false) ? "castle" : "town") + "_random_" + identifierUniqueness;
            newNodeComponent.Attributes["village_type"].Value = $"VillageType.{villageType}";
            newNodeComponent.Attributes["bound"].Value = $"Settlement.{bound?.StringId}";

            TextObject encyclopediaText = new TextObject("{=player_settlement_24}{SETTLEMENT_NAME} was founded by {HERO_NAME} of the {FACTION_TERM} on {BUILD_TIME}");
            encyclopediaText.SetTextVariable("SETTLEMENT_NAME", PlayerSettlementItem.EncyclopediaLinkWithName(newId, new TextObject(settlementName)));
            encyclopediaText.SetTextVariable("HERO_NAME", Hero.MainHero.EncyclopediaLinkWithName);
            encyclopediaText.SetTextVariable("FACTION_TERM", Hero.MainHero.Clan.EncyclopediaLinkWithName);
            encyclopediaText.SetTextVariable("BUILD_TIME", CampaignTime.Now.ToString());

            if (node.Attributes["text"] == null)
            {
                XmlAttribute encyclopediaTextAttribute = node.OwnerDocument.CreateAttribute("text");
                encyclopediaTextAttribute.Value = encyclopediaText.ToString();
                node.Attributes.SetNamedItem(encyclopediaTextAttribute);
            }
            else
            {
                node.Attributes["text"].Value = encyclopediaText.ToString();
            }

            var xml = $"<Settlements>{node.OuterXml}</Settlements>";

            villageItem = new PlayerSettlementItem
            {
                ItemXML = xml,
                Identifier = villageNumber,
                Type = (int) SettlementType.Village,
                SettlementName = settlementName,
                RotationMat3 = settlementPlacementFrame?.rotation,
                DeepEdits = new List<DeepTransformEdit>(deepEditPrefab == settlementVisualPrefab && deepTransformEdits != null ? deepTransformEdits : new()),
                Version = Main.Version,
                StringId = newId,
                PrefabId = item.Id
            };

            if (boundTarget == null)
            {
                if (PlayerSettlementInfo.Instance!.PlayerVillages == null)
                {
                    PlayerSettlementInfo.Instance.PlayerVillages = new();
                }
                PlayerSettlementInfo.Instance.PlayerVillages.Add(villageItem);
            }
            else
            {
                boundTarget.Villages!.Add(villageItem);
            }

            var doc = new XmlDocument();
            doc.LoadXml(xml);

            Campaign.Current.AsCampaignGameLoadingType(Campaign.GameLoadingType.NewCampaign, () =>
            {
                MBObjectManager.Instance.LoadXml(doc);
                return true;
            });


            var villageSettlement = MBObjectManager.Instance.GetObject<Settlement>(villageItem.StringId);
            villageItem.Settlement = villageSettlement;

            return villageSettlement;
        }

        private void PostNotablesAdded(Settlement settlement)
        {
            for (int i = 0; i < settlement.Notables.Count; i++)
            {
                Hero item = settlement.Notables[i];
                foreach (Hero lord in settlement.MapFaction.AliveLords)
                {
                    if (lord != item && lord == lord.Clan.Leader && lord.MapFaction == settlement.MapFaction)
                    {
                        float single = (float) HeroHelper.NPCPersonalityClashWithNPC(item, lord) * 0.01f * 2.5f;
                        float randomFloat = MBRandom.RandomFloat;
                        float mapDiagonal = Campaign.MapDiagonal;
                        foreach (Settlement s in lord.Clan.Settlements)
                        {
                            float single1 = (settlement == s ? 0f : s.GetPosition2D.Distance(settlement.GetPosition2D));
                            if (single1 >= mapDiagonal)
                            {
                                continue;
                            }
                            mapDiagonal = single1;
                        }
                        float single2 = (mapDiagonal < 100f ? 1f - mapDiagonal / 100f : 0f);
                        float randomFloat1 = single2 * MBRandom.RandomFloat + (1f - single2);
                        if (MBRandom.RandomFloat < 0.2f)
                        {
                            randomFloat1 = 1f / (0.5f + 0.5f * randomFloat1);
                        }
                        randomFloat *= randomFloat1;
                        if (randomFloat > 1f)
                        {
                            randomFloat = 1f;
                        }
                        this.DetermineRelation(item, lord, randomFloat, single);
                    }
                    for (int j = i + 1; j < settlement.Notables.Count; j++)
                    {
                        Hero hero = settlement.Notables[j];
                        float single3 = (float) HeroHelper.NPCPersonalityClashWithNPC(item, hero) * 0.01f * 2.5f;
                        float randomFloat2 = MBRandom.RandomFloat;
                        if (item.CharacterObject.Occupation == hero.CharacterObject.Occupation)
                        {
                            randomFloat2 = 1f - 0.25f * MBRandom.RandomFloat;
                        }
                        this.DetermineRelation(item, hero, randomFloat2, single3);
                    }
                }
            }
            int num = 50;
            for (int i1 = 0; i1 < num; i1++)
            {
                foreach (Hero allAliveHero in Hero.AllAliveHeroes)
                {
                    if (!allAliveHero.IsNotable)
                    {
                        continue;
                    }
                    this.UpdateNotableSupport(allAliveHero);
                }
            }
        }

        private void UpdateNotableSupport(Hero notable)
        {
            if (notable.SupporterOf != null)
            {
                int relation = notable.GetRelation(notable.SupporterOf.Leader);
                if (relation < 0)
                {
                    notable.SupporterOf = null;
                    return;
                }
                if (relation < 50)
                {
                    float single = (float) (50 - relation) / 500f;
                    if (MBRandom.RandomFloat < single)
                    {
                        notable.SupporterOf = null;
                    }
                }
            }
            else
            {
                foreach (Clan nonBanditFaction in Clan.NonBanditFactions)
                {
                    if (nonBanditFaction.Leader == null)
                    {
                        continue;
                    }
                    int num = notable.GetRelation(nonBanditFaction.Leader);
                    if (num <= 50)
                    {
                        continue;
                    }
                    float single1 = (float) (num - 50) / 2000f;
                    if (MBRandom.RandomFloat >= single1)
                    {
                        continue;
                    }
                    notable.SupporterOf = nonBanditFaction;
                }
            }
        }


        private void DetermineRelation(Hero hero1, Hero hero2, float randomValue, float chanceOfConflict)
        {
            float single = 0.3f;
            if (randomValue < single)
            {
                int num = (int) ((single - randomValue) * (single - randomValue) / (single * single) * 100f);
                if (num > 0)
                {
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(hero1, hero2, num, true);
                    return;
                }
            }
            else if (randomValue > 1f - chanceOfConflict)
            {
                int num1 = -(int) ((randomValue - (1f - chanceOfConflict)) * (randomValue - (1f - chanceOfConflict)) / (chanceOfConflict * chanceOfConflict) * 100f);
                if (num1 < 0)
                {
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(hero1, hero2, num1, true);
                }
            }
        }

        private void BuildTown()
        {
            gateSupported = true;
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;

            InformationManager.ShowTextInquiry(new TextInquiryData(new TextObject("{=player_settlement_02}Create Player Settlement").ToString(), new TextObject("{=player_settlement_03}What would you like to name your settlement?").ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                (string settlementName) =>
                {
                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;

                    if (string.IsNullOrEmpty(settlementName))
                    {
                        settlementName = new TextObject("{=player_settlement_n_01}Player Settlement").ToString();
                    }

                    void ApplyPlaced(string settlementName, CultureObject culture)
                    {
                        Settlement townSettlement = CreateTown(settlementName, culture, out PlayerSettlementItem townItem);

                        townSettlement.Town.OwnerClan = Hero.MainHero.Clan;

                        townSettlement.SetName(new TextObject(settlementName));

                        townSettlement.Party.SetLevelMaskIsDirty();
                        townSettlement.IsVisible = true;
                        townSettlement.IsInspected = true;
                        townSettlement.Town.FoodStocks = (float) townSettlement.Town.FoodStocksUpperLimit();
                        townSettlement.Party.SetVisualAsDirty();

                        SettlementVisualManager.Current.AddNewPartyVisualForParty(townSettlement.Party);

                        townSettlement.OnGameCreated();
                        townSettlement.AfterInitialized();
                        townSettlement.OnFinishLoadState();

                        var town = townSettlement.Town;

                        InitTownBuildings(townSettlement);

                        townItem.BuiltAt = Campaign.CurrentTime;

                        if (Main.Settings!.RequireGold)
                        {
                            GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, townSettlement, Main.Settings.RequiredGold, true);
                        }
                        else
                        {
                            townSettlement.SettlementComponent.ChangeGold(3_000);
                        }

                        //if (PlayerSettlementInfo.Instance.Towns.Count == 0 || PlayerSettlementInfo.Instance.Towns.Count == 1)
                        //{
                        //    // NB: This is to prevent leaking town details to older saves!
                        //    // Only for first town!
                        //    UpdateUniqueGameId();
                        //}
                        var campaignGameStarter = SandBoxManager.Instance.GameStarter;

                        if (Main.Settings.AddInitialGarrison)
                        {
                            townSettlement.AddGarrisonParty();
                            var garrisonTroopsCampaignBehavior = Campaign.Current.GetCampaignBehavior<GarrisonTroopsCampaignBehavior>();
                            if (garrisonTroopsCampaignBehavior != null && townSettlement.Town != null)
                            {
                                FillGarrisonPartyOnNewGameInvoker.Invoke(garrisonTroopsCampaignBehavior, new object[] { townSettlement.Town });
                            }
                            townSettlement.SetGarrisonWagePaymentLimit(Campaign.Current.Models.PartyWageModel.MaxWagePaymentLimit);
                        }

                        if (Main.Settings.AddInitialMilitia)
                        {
                            townSettlement.Militia = townSettlement.Town.MilitiaChange * 45f;
                        }

                        if (Main.Settings.AddInitialNotables)
                        {
                            int targetNotableCountForSettlement1 = Campaign.Current.Models.NotableSpawnModel.GetTargetNotableCountForSettlement(townSettlement, Occupation.Artisan);
                            for (int k = 0; k < targetNotableCountForSettlement1; k++)
                            {
                                HeroCreator.CreateNotable(Occupation.Artisan, townSettlement);
                            }
                            int x = Campaign.Current.Models.NotableSpawnModel.GetTargetNotableCountForSettlement(townSettlement, Occupation.Merchant);
                            for (int l = 0; l < x; l++)
                            {
                                HeroCreator.CreateNotable(Occupation.Merchant, townSettlement);
                            }
                            int targetNotableCountForSettlement2 = Campaign.Current.Models.NotableSpawnModel.GetTargetNotableCountForSettlement(townSettlement, Occupation.GangLeader);
                            for (int m = 0; m < targetNotableCountForSettlement2; m++)
                            {
                                HeroCreator.CreateNotable(Occupation.GangLeader, townSettlement);
                            }

                            PostNotablesAdded(townSettlement);
                        }

                        foreach (ItemCategory all in ItemCategories.All)
                        {
                            if (!all.IsValid)
                            {
                                continue;
                            }
                            town.MarketData.AddDemand(all, 3f);
                            town.MarketData.AddSupply(all, 2f);
                        }

                        town.MarketData.UpdateStores();

                        if ((Main.Settings.AddInitialNotables) && townSettlement.Notables.Count > 0)
                        {
                            var b = campaignGameStarter.CampaignBehaviors.FirstOrDefault(b => b is WorkshopsCampaignBehavior) as WorkshopsCampaignBehavior;
                            if (b is WorkshopsCampaignBehavior workshopsCampaignBehavior)
                            {
                                workshopsCampaignBehavior.NewTownBuilt(town);
                            }

                            int num0 = MBRandom.RandomInt(0, townSettlement.Alleys.Count);
                            IEnumerable<Hero> notables =
                                from x in townSettlement.Notables
                                where x.IsGangLeader
                                select x;
                            for (int i = num0; i < num0 + 2; i++)
                            {
                                townSettlement.Alleys[i % townSettlement.Alleys.Count].SetOwner(notables.ElementAt<Hero>(i % notables.Count<Hero>()));
                            }
                        }


                        var craftingCampaignBehavior = campaignGameStarter.CampaignBehaviors.FirstOrDefault<CampaignBehaviorBase>(b => b is CraftingCampaignBehavior) as CraftingCampaignBehavior;
                        craftingCampaignBehavior?.AddTown(town, out _);

                        var rc = campaignGameStarter.CampaignBehaviors.FirstOrDefault(b => b is RecruitmentCampaignBehavior) as RecruitmentCampaignBehavior;
                        if (rc is RecruitmentCampaignBehavior recruitmentCampaignBehavior)
                        {
                            recruitmentCampaignBehavior.NewSettlementBuilt(townSettlement);
                        }


                        _settlementCreated.Invoke(townItem.Settlement);

                        //Reset();
                        SaveHandler.SaveLoad(!Main.Settings.CreateNewSave);
                    }


                    void Apply(string settlementName, CultureObject culture)
                    {
                        settlementPlacementFrame = null;

                        void ConfirmAndApply()
                        {
                            var createPlayerSettlementText = new TextObject("{=player_settlement_04}Build a Town").ToString();
                            var confirm = new TextObject("{=player_settlement_05}Are you sure you want to build your town here?");

                            InformationManager.ShowInquiry(new InquiryData(createPlayerSettlementText, confirm.ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                                () =>
                                {
                                    InformationManager.HideInquiry();
                                    ApplyPlaced(settlementName, culture);
                                },
                                () =>
                                {
                                    // Cancelled. Do nothing.
                                    InformationManager.HideInquiry();

                                    // If not in placement, we have to reset completely. Otherwise we can just return to placement
                                    if (!Main.Settings!.SettlementPlacement)
                                    {
                                        Reset();
                                        MapBarExtensionVM.Current?.OnRefresh();
                                    }
                                }), true, false);
                        }

                        availableModels?.Clear();
                        if (Main.Settings!.SelectedCultureOnly && Main.Submodule!.CultureTemplates.ContainsKey(culture.StringId))
                        {
                            availableModels = Main.Submodule.CultureTemplates[culture.StringId].SelectMany(SelectTownTemplates).ToList();
                            currentModelOptionIdx = -1;
                        }

                        if (availableModels == null || availableModels.Count == 0)
                        {
                            availableModels = Main.Submodule!.CultureTemplates.Values.SelectMany(c => c.SelectMany(SelectTownTemplates)).ToList();
                            currentModelOptionIdx = availableModels.FindIndex(a => a.Culture == culture.StringId) - 1;
                        }

                        if (!Main.Settings!.SettlementPlacement)
                        {
                            ConfirmAndApply();
                            return;
                        }

                        StartSettlementPlacement();

                        applyPending = () => ConfirmAndApply();
                    }

                    if (Main.Settings!.ForcePlayerCulture)
                    {
                        Apply(settlementName, Hero.MainHero.Culture);
                        return;
                    }

                    var titleText = new TextObject("{=player_settlement_09}Choose town culture");
                    var descriptionText = new TextObject("{=player_settlement_10}Choose the culture for {TOWN}");
                    descriptionText.SetTextVariable("TOWN", settlementName);


                    List<InquiryElement> inquiryElements1 = GetCultures(true).Select(c => new InquiryElement(c, c.Name.ToString(), new BannerImageIdentifier(new Banner(c.Banner)), true, c.Name.ToString())).ToList();

                    MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                        titleText: titleText.ToString(),
                        descriptionText: descriptionText.ToString(),
                        inquiryElements: inquiryElements1,
                        isExitShown: false,
                        maxSelectableOptionCount: 1,
                        minSelectableOptionCount: 1,
                        affirmativeText: GameTexts.FindText("str_ok", null).ToString(),
                        negativeText: null,
                        affirmativeAction: (List<InquiryElement> args) =>
                        {
                            List<InquiryElement> source = args;
                            //InformationManager.HideInquiry();

                            CultureObject culture = (args?.FirstOrDefault()?.Identifier as CultureObject) ?? Hero.MainHero.Culture;

                            Apply(settlementName, culture);
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
                    Reset();
                    MapBarExtensionVM.Current?.OnRefresh();
                }, false, new Func<string, Tuple<bool, string>>(FactionHelper.IsKingdomNameApplicable), "", ""), true, false);
        }
        List<PlayerSettlementItemTemplate> SelectTownTemplates(CultureSettlementTemplate cst)
        {
            var templates = new List<PlayerSettlementItemTemplate>();
            try
            {
                var nodes = cst.Document.SelectNodes($"descendant::Settlement[@template_type='Town']").OfType<XmlNode>();

                foreach (var node in nodes)
                {
                    var id = node.Attributes["id"].Value;

                    if (Main.BlacklistedTemplates.Contains(id))
                    {
                        LogManager.EventTracer.Trace($"Skipped blacklisted template: {id}");
                        continue;
                    }

                    templates.Add(new PlayerSettlementItemTemplate
                    {
                        Id = id,
                        ItemXML = node,
                        Type = (int) SettlementType.Town,
                        Culture = cst.CultureId
                    });
                }
            }
            catch (Exception e)
            {
                LogManager.Log.NotifyBad(e);
            }
            return templates;
        }

        private void StartSettlementPlacement()
        {
            UpdateSettlementVisualEntity(true, retry: true);

            ShowSettlementPlacementHelp();
        }

        private static void ShowSettlementPlacementHelp(bool forceShow = false)
        {
            if (Main.Settings!.DisableAutoHints && !forceShow)
            {
                return;
            }
            TextObject settlementPlacementMessage = new TextObject("{=player_settlement_37}Choose your settlement. \r\nPress {HELP_KEY} for help. \r\nClick {MOUSE_CLICK} anywhere to apply or press {ESC_KEY} to cancel.  \r\nUse {DEEP_EDIT_KEY} to switch to deep edit mode. \r\nUse {CYCLE_MODIFIER_KEY} and {CYCLE_BACK_KEY} / {CYCLE_NEXT_KEY} to change visual options.\r\nUse {ROTATE_MODIFIER_KEY} and {ROTATE_BACK_KEY} / {ROTATE_NEXT_KEY} to change rotation. \r\nUse {ROTATE_MODIFIER_KEY} and {ROTATE_FORWARD_KEY} / {ROTATE_BACKWARD_KEY} to change forward rotation. \r\nUse {ROTATE_MODIFIER_KEY} + {ROTATE_MODIFIER_ALTERNATE} and {ROTATE_FORWARD_KEY} / {ROTATE_BACKWARD_KEY} to change axis rotation. \r\nUse {SCALE_MODIFIER_KEY} and {SCALE_BACK_KEY} / {SCALE_NEXT_KEY} to change scale. \r\nUse {CYCLE_MODIFIER_KEY} and {MOVE_UP_KEY} / {MOVE_DOWN_KEY} to move up or down.");
            settlementPlacementMessage.SetTextVariable("HELP_KEY", TaleWorlds.Core.HyperlinkTexts.GetKeyHyperlinkText(Main.Submodule!.HelpKey.GetInputKey().ToString()));
            settlementPlacementMessage.SetTextVariable("ESC_KEY", TaleWorlds.Core.HyperlinkTexts.GetKeyHyperlinkText(InputKey.Escape.ToString()));
            settlementPlacementMessage.SetTextVariable("MOUSE_CLICK", HyperlinkTexts.GetKeyHyperlinkText(InputKey.LeftMouseButton.ToString()));
            settlementPlacementMessage.SetTextVariable("DEEP_EDIT_KEY", TaleWorlds.Core.HyperlinkTexts.GetKeyHyperlinkText(Main.Submodule!.DeepEditToggleKey.GetInputKey().ToString()));
            settlementPlacementMessage.SetTextVariable("CYCLE_MODIFIER_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.CycleModifierKey).ToString()));
            settlementPlacementMessage.SetTextVariable("CYCLE_BACK_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.CycleBackKey).ToString()));
            settlementPlacementMessage.SetTextVariable("CYCLE_NEXT_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.CycleNextKey).ToString()));
            settlementPlacementMessage.SetTextVariable("MOVE_UP_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.MoveUpKey).ToString()));
            settlementPlacementMessage.SetTextVariable("MOVE_DOWN_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.MoveDownKey).ToString()));
            settlementPlacementMessage.SetTextVariable("ROTATE_MODIFIER_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.RotateModifierKey).ToString()));
            settlementPlacementMessage.SetTextVariable("ROTATE_MODIFIER_ALTERNATE", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.RotateAlternateModifierKey).ToString()));
            settlementPlacementMessage.SetTextVariable("ROTATE_BACK_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.RotatePreviousKey).ToString()));
            settlementPlacementMessage.SetTextVariable("ROTATE_NEXT_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.RotateNextKey).ToString()));
            settlementPlacementMessage.SetTextVariable("ROTATE_FORWARD_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.RotateForwardKey).ToString()));
            settlementPlacementMessage.SetTextVariable("ROTATE_BACKWARD_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.RotateBackwardsKey).ToString()));
            settlementPlacementMessage.SetTextVariable("SCALE_MODIFIER_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.ScaleModifierKey).ToString()));
            settlementPlacementMessage.SetTextVariable("SCALE_BACK_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.ScaleSmallerKey).ToString()));
            settlementPlacementMessage.SetTextVariable("SCALE_NEXT_KEY", HyperlinkTexts.GetKeyHyperlinkText(((GameKey) Main.Submodule!.ScaleBiggerKey).ToString()));
            MBInformationManager.AddQuickInformation(settlementPlacementMessage, Main.Settings.HintDurationSeconds * 1000, Hero.MainHero?.CharacterObject);
        }

        private static void InitTownBuildings(Settlement townSettlement)
        {
            var town = townSettlement.Town;

            foreach (BuildingType all1 in BuildingType.All)
            {
                // TODO: Config default building level?
                int num1 = MBRandom.RandomInt(1, 4);
                //LogManager.Log.Info($"Considering building type for default military project: {all1.StringId}-{all1.Name}-{all1.Id}-{all1.IsDailyProject}-{all1.IsMilitaryProject}-{all1.ToString()}");

                if (town.Buildings.Any<Building>((Building k) => k.BuildingType.StringId == all1.StringId))
                {
                    continue;
                }
                // TODO: add check has harbor and add as possible building: building_shipyard
                if (!all1.StringId.StartsWith("building_settlement"))
                {
                    continue;
                }


                town.Buildings.Add(new Building(all1, town, 0f, all1.IsDailyProject ? 1 : num1));
            }

            foreach (Building building1 in
                from k in town.Buildings
                orderby k.CurrentLevel descending
                select k)
            {
                if (building1.CurrentLevel == 3 || building1.CurrentLevel == building1.BuildingType.StartLevel || building1.BuildingType.IsDailyProject)
                {
                    continue;
                }
                town.BuildingsInProgress.Enqueue(building1);
            }

            Building dailyDefault = townSettlement.Town.Buildings.FirstOrDefault(b => b.IsCurrentlyDefault);
            if (dailyDefault == null || !dailyDefault.BuildingType.IsDailyProject)
            {
                dailyDefault = townSettlement.Town.Buildings.FirstOrDefault(b => b.BuildingType.IsDailyProject);
                BuildingHelper.ChangeDefaultBuilding(dailyDefault, townSettlement.Town);
                dailyDefault.IsCurrentlyDefault = true;
            }

            //if (townSettlement.Town.BuildingsInProgress.IsEmpty() && townSettlement.Town.CurrentDefaultBuilding == null)
            //{
            //    BuildingHelper.ChangeDefaultBuilding(townSettlement.Town.Buildings.FirstOrDefault<Building>(), townSettlement.Town);
            //}
        }

        private Settlement CreateTown(string settlementName, CultureObject culture, out PlayerSettlementItem townItem)
        {
            if (currentModelOptionIdx < 0)
            {
                currentModelOptionIdx = new Random().Next(0, availableModels.Count);
            }

            if (PlayerSettlementInfo.Instance!.Towns == null)
            {
                PlayerSettlementInfo.Instance!.Towns = new();
            }

            var atPos = settlementPlacementFrame?.origin != null ? settlementPlacementFrame.Value.origin.AsVec2 : MobileParty.MainParty.GetPosition2D;

            var townNumber = PlayerSettlementInfo.Instance!.Towns!.Count + 1;

            // For now gate position is the same as the main position.
            var gPos = atPos;
            var item = availableModels[currentModelOptionIdx];

            var node = item.ItemXML.CloneNode(true);

            // If a gate position has been placed, use that instead.
            if (gateSupported && Main.Settings!.AllowGatePosition && gatePlacementFrame != null)
            {
                gPos = gatePlacementFrame?.origin != null ? gatePlacementFrame.Value.origin.AsVec2 : gPos;

                //gate_posX = "{{G_POS_X}}"
                if (node.Attributes["gate_posX"] == null)
                {
                    XmlAttribute gatePosXAttribute = node.OwnerDocument.CreateAttribute("gate_posX");
                    gatePosXAttribute.Value = gPos.X.ToString();
                    node.Attributes.SetNamedItem(gatePosXAttribute);
                }
                else
                {
                    node.Attributes["gate_posX"].Value = gPos.X.ToString();
                }
                //gate_posY = "{{G_POS_Y}}"
                if (node.Attributes["gate_posY"] == null)
                {
                    XmlAttribute gatePosYAttribute = node.OwnerDocument.CreateAttribute("gate_posY");
                    gatePosYAttribute.Value = gPos.Y.ToString();
                    node.Attributes.SetNamedItem(gatePosYAttribute);
                }
                else
                {
                    node.Attributes["gate_posY"].Value = gPos.Y.ToString();
                }
            }


            string identifierUniqueness = MBRandom.RandomInt().ToString();
            var newId = item.Id + "_random_" + identifierUniqueness;

            node.Attributes["id"].Value = newId;

            var nodeComponent = node.SelectSingleNode("descendant::Town");
            nodeComponent.Attributes["id"].Value = newId + "_town_comp";

            node.Attributes["posX"].Value = atPos.X.ToString();
            node.Attributes["posY"].Value = atPos.Y.ToString();
            node.Attributes["name"].Value = settlementName;
            node.Attributes["owner"].Value = $"Faction.{Hero.MainHero.Clan.StringId}";
            node.Attributes["culture"].Value = $"Culture.{culture.StringId}";

            TextObject encyclopediaText = new TextObject("{=player_settlement_24}{SETTLEMENT_NAME} was founded by {HERO_NAME} of the {FACTION_TERM} on {BUILD_TIME}");
            encyclopediaText.SetTextVariable("SETTLEMENT_NAME", PlayerSettlementItem.EncyclopediaLinkWithName(newId, new TextObject(settlementName)));
            encyclopediaText.SetTextVariable("HERO_NAME", Hero.MainHero.EncyclopediaLinkWithName);
            encyclopediaText.SetTextVariable("FACTION_TERM", Hero.MainHero.Clan.EncyclopediaLinkWithName);
            encyclopediaText.SetTextVariable("BUILD_TIME", CampaignTime.Now.ToString());

            if (node.Attributes["text"] == null)
            {
                XmlAttribute encyclopediaTextAttribute = node.OwnerDocument.CreateAttribute("text");
                encyclopediaTextAttribute.Value = encyclopediaText.ToString();
                node.Attributes.SetNamedItem(encyclopediaTextAttribute);
            }
            else
            {
                node.Attributes["text"].Value = encyclopediaText.ToString();
            }

            var xml = $"<Settlements>{node.OuterXml}</Settlements>";
            xml = xml.Replace("{{G_POS_X}}", (gPos.X).ToString());
            xml = xml.Replace("{{G_POS_Y}}", (gPos.Y).ToString());

            townItem = new PlayerSettlementItem
            {
                ItemXML = xml,
                Identifier = townNumber,
                Type = (int) SettlementType.Town,
                SettlementName = settlementName,
                RotationMat3 = settlementPlacementFrame?.rotation,
                DeepEdits = new List<DeepTransformEdit>(deepEditPrefab == settlementVisualPrefab && deepTransformEdits != null ? deepTransformEdits : new()),
                Version = Main.Version,
                StringId = newId,
                PrefabId = item.Id
            };
            PlayerSettlementInfo.Instance.Towns.Add(townItem);

            var doc = new XmlDocument();
            doc.LoadXml(xml);

            Campaign.Current.AsCampaignGameLoadingType(Campaign.GameLoadingType.NewCampaign, () =>
            {
                MBObjectManager.Instance.LoadXml(doc);
                return true;
            });

            var townSettlement = MBObjectManager.Instance.GetObject<Settlement>(townItem.StringId);
            townItem.Settlement = townSettlement;

            return townSettlement;
        }

        public void RefreshVisualSelection()
        {
            deepEditScale = 1f;
            currentDeepTarget = null;

            currentModelOptionIdx -= 1;
            UpdateSettlementVisualEntity(forward: true, retry: true);
        }

        static Exception? previousVisualUpdateException = null;

        private void UpdateSettlementVisualEntity(bool forward, bool retry = false)
        {
            try
            {
                LogManager.EventTracer.Trace($"UpdateSettlementVisualEntity forward={forward} noRetry={retry}");
                if (forward)
                {
                    currentModelOptionIdx += 1;
                    if (currentModelOptionIdx >= availableModels.Count)
                    {
                        currentModelOptionIdx = 0;
                    }
                }
                else
                {
                    currentModelOptionIdx -= 1;
                    if (currentModelOptionIdx < 0)
                    {
                        currentModelOptionIdx = availableModels.Count - 1;
                    }
                }

                if (currentModelOptionIdx < 0)
                {
                    currentModelOptionIdx = 0;
                }

                ClearEntities();

                var atPos = settlementPlacementFrame?.origin != null ? settlementPlacementFrame.Value.origin.AsVec2 : MobileParty.MainParty.GetPosition2D;

                var template = availableModels[currentModelOptionIdx];

                Debug.Print($"Requesting swap model for settlement build to: {template.Id}", 2, Debug.DebugColor.Purple);
                var traceDetail = new List<string>
                {
                    $"Requesting swap model for settlement build to: {template.Id}",
                    $"Available models: {availableModels.Count}",
                };
                traceDetail.AddRange(availableModels.Select((a, idx) => $"\t\t{a.Id} - Culture: '{a.Culture}', Type: '{a.Type}', Variant: '{idx + 1}'"));
                LogManager.EventTracer.Trace(traceDetail);

                var mapScene = ((MapScene) Campaign.Current.MapSceneWrapper).Scene;
                Vec2 position2D = atPos;

                string prefabId = template.Id;
                string entityId = GhostSettlementEntityId;
                settlementVisualEntity = Campaign.Current.MapSceneWrapper.AddPrefabEntityToMapScene(ref mapScene, ref entityId, ref position2D, ref prefabId);
                var settlementVisualEntity2 = mapScene.GetCampaignEntityWithName(GhostSettlementEntityId);
                if (settlementVisualEntity != settlementVisualEntity2)
                {
                    LogManager.EventTracer.Trace($"settlementVisualEntity != settlementVisualEntity2 - Prefab: '{prefabId}', Entity: '{GhostSettlementEntityId}'");
                }
                settlementVisualEntity?.AddBodyFlags(BodyFlags.DoNotCollideWithRaycast | BodyFlags.DontCollideWithCamera | BodyFlags.DontTransferToPhysicsEngine | BodyFlags.CommonCollisionExcludeFlagsForEditor | BodyFlags.CommonCollisionExcludeFlags | BodyFlags.CommonFocusRayCastExcludeFlags | BodyFlags.CommonFlagsThatDoNotBlockRay);
                previousVisualUpdateException = null;

                if (settlementVisualEntity == null)
                {
                    Reset();
                }
                settlementVisualEntityChildren.Clear();

                // Recursiveley gather all submodels for deep editing
                settlementVisualEntity?.GetChildrenRecursive(ref settlementVisualEntityChildren);
                settlementVisualPrefab = prefabId;

                if (settlementVisualPrefab != deepEditPrefab)
                {
                    ResetDeepEdits();
                }
            }
            catch (Exception e)
            {
                bool rethrow = previousVisualUpdateException != null;
                if (!rethrow)
                {
                    previousVisualUpdateException = e;
                }
                LogManager.Log.NotifyBad(e);
                if (retry)
                {
                    // Retry once without allowing another retry to avoid stackoverflow loops
                    UpdateSettlementVisualEntity(forward, retry: false);
                }
                else if ((e is AccessViolationException || previousVisualUpdateException is AccessViolationException))
                {
                    var title = new TextObject("{=player_settlement_30}Corrupt Template").ToString();
                    var confirm = new TextObject("{=player_settlement_31}Player Settlements has encountered a corrupt template. \nPlease screenshot and report this along with the log file at '{LOG_PATH}'. \n{ERROR_DETAIL}\n\nYour game will now have a new emergency save created to avoid crashing. It is highly recommended to close the application before loading.");
                    confirm.SetTextVariable("LOG_PATH", LogManager.Log.LogPath);
                    string errorDetail = "\r\n" + previousVisualUpdateException!.Message;
                    try
                    {
                        var a = availableModels[currentModelOptionIdx];
                        errorDetail += $"\r\n\r\nTemplate: {a.Id} - Culture: '{a.Culture}', Type: '{a.Type}'";
                        Main.Submodule?.UpdateBlacklist(a.Id);
                    }
                    catch (Exception) { }
                    confirm.SetTextVariable("ERROR_DETAIL", errorDetail);

                    var close = new TextObject("{=player_settlement_32}Close Game");
                    var recover = new TextObject("{=player_settlement_33}Attempt Recovery");

                    InformationManager.ShowInquiry(new InquiryData(title, confirm.ToString(), true, !CampaignOptions.IsIronmanMode, close.ToString(), recover.ToString(),
                        () =>
                        {
                            InformationManager.HideInquiry();
                            if (!CampaignOptions.IsIronmanMode)
                            {
                                CampaignEvents.OnSaveOverEvent.AddNonSerializedListener(this, (b, s) => Utilities.QuitGame());
                                SaveHandler.SaveOnly(overwrite: false);
                            }
                            else
                            {
                                Utilities.QuitGame();
                            }
                        },
                        () =>
                        {
                            InformationManager.HideInquiry();
                            if (!CampaignOptions.IsIronmanMode)
                            {
                                SaveHandler.SaveLoad(overwrite: false);
                            }
                            else
                            {
                                Utilities.QuitGame();
                            }
                        }), true, false);
                }
                else if (rethrow)
                {
                    throw;
                }
            }
        }

        private void ShowGhostGateVisualEntity(bool retry = false)
        {
            try
            {
                LogManager.EventTracer.Trace($"ShowGhostGateVisualEntity noRetry={retry}");

                //ClearEntities();
                gatePlacementFrame = null;

                Debug.Print($"Requesting swap model for gate ghost build to: {GhostGateEntityId}", 2, Debug.DebugColor.Purple);
                LogManager.EventTracer.Trace($"Requesting swap model for gate ghost build to: {GhostGateEntityId}");

                var mapScene = ((MapScene) Campaign.Current.MapSceneWrapper).Scene;
                Vec2 position2D = MobileParty.MainParty.GetPosition2D;

                string prefabId = GhostGatePrefabId;
                string entityId = GhostGateEntityId;
                ghostGateVisualEntity = Campaign.Current.MapSceneWrapper.AddPrefabEntityToMapScene(ref mapScene, ref entityId, ref position2D, ref prefabId);
                var ghostEntity2 = mapScene.GetCampaignEntityWithName(GhostGateEntityId);
                if (ghostGateVisualEntity != ghostEntity2)
                {
                    LogManager.EventTracer.Trace($"settlementVisualEntity != settlementVisualEntity2 - Prefab: '{prefabId}', Entity: '{entityId}'");
                }
                ghostGateVisualEntity?.AddBodyFlags(BodyFlags.DoNotCollideWithRaycast | BodyFlags.DontCollideWithCamera | BodyFlags.DontTransferToPhysicsEngine | BodyFlags.CommonCollisionExcludeFlagsForEditor | BodyFlags.CommonCollisionExcludeFlags | BodyFlags.CommonFocusRayCastExcludeFlags | BodyFlags.CommonFlagsThatDoNotBlockRay);
                previousVisualUpdateException = null;

                if (ghostGateVisualEntity == null)
                {
                    // Cannot place gate, going straight to apply
                    ClearEntities();
                    ApplyNow();
                }
            }
            catch (Exception e)
            {
                bool rethrow = previousVisualUpdateException != null;
                if (!rethrow)
                {
                    previousVisualUpdateException = e;
                }
                LogManager.Log.NotifyBad(e);
                if (retry)
                {
                    // Retry once without allowing another retry to avoid stackoverflow loops
                    ShowGhostGateVisualEntity(retry: false);
                }
                else if (rethrow)
                {
                    throw;
                }
            }
        }

        private List<InquiryElement> GetVillageTypeInquiry()
        {
            List<InquiryElement> inquiryElements = new();
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.BattanianHorseRanch, DefaultVillageTypes.BattanianHorseRanch.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.BattanianHorseRanch.PrimaryProduction), true, DefaultVillageTypes.BattanianHorseRanch.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.CattleRange, DefaultVillageTypes.CattleRange.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.CattleRange.PrimaryProduction), true, DefaultVillageTypes.CattleRange.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.ClayMine, DefaultVillageTypes.ClayMine.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.ClayMine.PrimaryProduction), true, DefaultVillageTypes.ClayMine.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.DateFarm, DefaultVillageTypes.DateFarm.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.DateFarm.PrimaryProduction), true, DefaultVillageTypes.DateFarm.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.DesertHorseRanch, DefaultVillageTypes.DesertHorseRanch.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.DesertHorseRanch.PrimaryProduction), true, DefaultVillageTypes.DesertHorseRanch.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.EuropeHorseRanch, DefaultVillageTypes.EuropeHorseRanch.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.EuropeHorseRanch.PrimaryProduction), true, DefaultVillageTypes.EuropeHorseRanch.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.Fisherman, DefaultVillageTypes.Fisherman.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.Fisherman.PrimaryProduction), true, DefaultVillageTypes.Fisherman.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.FlaxPlant, DefaultVillageTypes.FlaxPlant.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.FlaxPlant.PrimaryProduction), true, DefaultVillageTypes.FlaxPlant.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.HogFarm, DefaultVillageTypes.HogFarm.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.HogFarm.PrimaryProduction), true, DefaultVillageTypes.HogFarm.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.IronMine, DefaultVillageTypes.IronMine.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.IronMine.PrimaryProduction), true, DefaultVillageTypes.IronMine.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.Lumberjack, DefaultVillageTypes.Lumberjack.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.Lumberjack.PrimaryProduction), true, DefaultVillageTypes.Lumberjack.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.OliveTrees, DefaultVillageTypes.OliveTrees.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.OliveTrees.PrimaryProduction), true, DefaultVillageTypes.OliveTrees.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.SaltMine, DefaultVillageTypes.SaltMine.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.SaltMine.PrimaryProduction), true, DefaultVillageTypes.SaltMine.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.SheepFarm, DefaultVillageTypes.SheepFarm.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.SheepFarm.PrimaryProduction), true, DefaultVillageTypes.SheepFarm.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.SilkPlant, DefaultVillageTypes.SilkPlant.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.SilkPlant.PrimaryProduction), true, DefaultVillageTypes.SilkPlant.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.SilverMine, DefaultVillageTypes.SilverMine.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.SilverMine.PrimaryProduction), true, DefaultVillageTypes.SilverMine.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.SteppeHorseRanch, DefaultVillageTypes.SteppeHorseRanch.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.SteppeHorseRanch.PrimaryProduction), true, DefaultVillageTypes.SteppeHorseRanch.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.SturgianHorseRanch, DefaultVillageTypes.SturgianHorseRanch.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.SturgianHorseRanch.PrimaryProduction), true, DefaultVillageTypes.SturgianHorseRanch.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.VineYard, DefaultVillageTypes.VineYard.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.VineYard.PrimaryProduction), true, DefaultVillageTypes.VineYard.ShortName.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.VlandianHorseRanch, DefaultVillageTypes.VlandianHorseRanch.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.VlandianHorseRanch.PrimaryProduction), true, DefaultVillageTypes.VlandianHorseRanch.PrimaryProduction.Name.ToString()));
            inquiryElements.Add(new InquiryElement(DefaultVillageTypes.WheatFarm, DefaultVillageTypes.WheatFarm.ShortName.ToString(), new ItemImageIdentifier(DefaultVillageTypes.WheatFarm.PrimaryProduction), true, DefaultVillageTypes.WheatFarm.PrimaryProduction.Name.ToString()));

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
            catch (Exception e)
            {
                LogManager.Log.NotifyBad(e);
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

        public IEnumerable<Settlement?> GetPotentialVillageBoundOwners()
        {
            if (PlayerSettlementInfo.Instance == null || Main.Settings == null || PlayerSettlementInfo.Instance.TotalVillages >= Settings.HardMaxVillages)
            {
                return new List<Settlement>();
            }

            if ((Hero.MainHero?.Clan?.Settlements) != null && Hero.MainHero.Clan.Settlements.Count != 0)
            {
                var incompletePlayerOwnedGameSettlements = Hero.MainHero.Clan.Settlements.Where(s => (s.IsTown && (s.BoundVillages?.Count ?? 0) < Main.Settings.MaxVillagesPerTown) || (s.IsCastle && (s.BoundVillages?.Count ?? 0) < Main.Settings.MaxVillagesPerCastle));

                return incompletePlayerOwnedGameSettlements ?? new List<Settlement>();
            }
            return new List<Settlement>();

            // TODO: If player can build for previously owned but lost...
            //if (PlayerSettlementInfo.Instance.Towns == null)
            //{
            //    PlayerSettlementInfo.Instance.Towns = new();
            //}

            //if (PlayerSettlementInfo.Instance.PlayerVillages == null)
            //{
            //    PlayerSettlementInfo.Instance.PlayerVillages = new();
            //}

            //if (PlayerSettlementInfo.Instance.Castles == null)
            //{
            //    PlayerSettlementInfo.Instance.Castles = new();
            //}

            //var incompleteTowns = PlayerSettlementInfo.Instance.Towns.Where(t => t.Villages.Count < Main.Settings.MaxVillagesPerTown && t.Settlement != null).Select(t => t.Settlement);

            //var incompleteCastles = PlayerSettlementInfo.Instance.Castles.Where(c => c.Villages.Count < Main.Settings.MaxVillagesPerCastle && c.Settlement != null).Select(t => t.Settlement);



            //return incompleteTowns.Union(incompleteCastles) ?? new List<Settlement>();
        }

        static readonly FastInvokeHandler SetUniqueGameId = MethodInvoker.GetHandler(AccessTools.Property(typeof(Campaign), nameof(Campaign.UniqueGameId)).SetMethod);
        public static (string oldId, string newId) UpdateUniqueGameId()
        {
            var oldId = Campaign.Current.UniqueGameId;
            var newId = MiscHelper.GenerateCampaignId(12);
            SetUniqueGameId(Campaign.Current, newId);

            return (oldId, Campaign.Current.UniqueGameId);
        }

        public Settlement? CalculateVillageOwner(/*out PlayerSettlementItem? boundTarget*/)
        {
            if (PlayerSettlementInfo.Instance == null || Main.Settings == null || PlayerSettlementInfo.Instance.TotalVillages >= Settings.HardMaxVillages)
            {
                //boundTarget = null;
                return null;
            }

            if (PlayerSettlementInfo.Instance.PlayerVillages == null)
            {
                PlayerSettlementInfo.Instance.PlayerVillages = new();
            }

            if (PlayerSettlementInfo.Instance.Towns == null)
            {
                PlayerSettlementInfo.Instance.Towns = new();
            }

            if (PlayerSettlementInfo.Instance.Castles == null)
            {
                PlayerSettlementInfo.Instance.Castles = new();
            }

            //var incompleteTown = PlayerSettlementInfo.Instance.Towns.FirstOrDefault(t => t.Villages.Count < Main.Settings.MaxVillagesPerTown);
            //if (incompleteTown != null)
            //{
            //    boundTarget = incompleteTown;
            //    return boundTarget.Settlement;
            //}

            //var incompleteCastle = PlayerSettlementInfo.Instance.Castles.FirstOrDefault(t => t.Villages.Count < Main.Settings.MaxVillagesPerCastle);
            //if (incompleteCastle != null)
            //{
            //    boundTarget = incompleteCastle;
            //    return boundTarget.Settlement;
            //}

            if (Hero.MainHero?.Clan?.Settlements == null || Hero.MainHero.Clan.Settlements.Count == 0)
            {
                //boundTarget = null;
                return null;
            }

            var incompletePlayerOwnedGameSettlement = Hero.MainHero.Clan.Settlements.FirstOrDefault(s => (s.IsTown && (s.BoundVillages?.Count ?? 0) < Main.Settings.MaxVillagesPerTown) || (s.IsCastle && (s.BoundVillages?.Count ?? 0) < Main.Settings.MaxVillagesPerCastle));
            //boundTarget = null;
            return incompletePlayerOwnedGameSettlement;
        }
        #endregion
    }
}