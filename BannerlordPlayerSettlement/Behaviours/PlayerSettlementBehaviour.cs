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

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Overlay;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
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

        public SettlementType SettlementRequest = SettlementType.None;


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
                       PlayerSettlementInfo.Instance.TotalVillages >= Main.Settings.HardMaxVillages;
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

        private List<PlayerSettlementItemTemplate> availableModels = new List<PlayerSettlementItemTemplate>();
        private int currentModelOptionIdx = -1;
        private GameEntity? settlementVisualEntity = null;
        private MatrixFrame? settlementPlacementFrame = null;
        private float settlementRotationBearing = 0f;
        private float settlementRotationVelocity = 0f;
        private Action? applyPending = null;
        private float holdTime = 0f;

        public bool IsPlacingSettlement => settlementVisualEntity != null && applyPending != null;

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
                    var extraVillages = PlayerSettlementInfo.Instance.PlayerVillages;
                    if (extraVillages == null)
                    {
                        extraVillages = (PlayerSettlementInfo.Instance.PlayerVillages = new List<PlayerSettlementItem>());
                    }

                    for (int t = 0; t < extraVillages.Count; t++)
                    {
                        var village = extraVillages[t];
                        if (!village.BuildComplete && !village.BuildEnd.IsFuture)
                        {
                            NotifyComplete(village);
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

        public void NotifyComplete(PlayerSettlementItem item)
        {
            item.BuildComplete = true;
            TextObject message = new TextObject("{=player_settlement_07}{TOWN} construction has completed!", null);
            message.SetTextVariable("TOWN", item.SettlementName);
            MBInformationManager.AddQuickInformation(message, 0, null, "");
            InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Colours.Green));

            _settlementBuildComplete.Invoke(item.Settlement!);
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
        }

        public void OnBeforeTick(ref MapCameraView.InputInformation inputInformation)
        {
            if (Main.Settings != null && Main.Settings.Enabled && PlayerSettlementInfo.Instance != null)
            {
                if (settlementVisualEntity != null)
                {
                    if (Game.Current.GameStateManager.ActiveState is not MapState mapState)
                    {
                        return;
                    }
                    if (mapState.Handler is not MapScreen mapScreen)
                    {
                        return;
                    }

                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                    Campaign.Current.SetTimeControlModeLock(true);

                    var cycleModifierDown = mapScreen.SceneLayer.Input.IsShiftDown();
                    if (cycleModifierDown)
                    {
                        var cycleForwardRelease = mapScreen.SceneLayer.Input.IsGameKeyDownAndReleased(57);
                        var cycleBackRelease = mapScreen.SceneLayer.Input.IsGameKeyDownAndReleased(58);

                        var cycleForwardHeld = mapScreen.SceneLayer.Input.IsGameKeyDown(57);
                        var cycleBackHeld = mapScreen.SceneLayer.Input.IsGameKeyDown(58);

                        var holdWait = 1 / Main.Settings.CycleSpeed;

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
                            }
                        }
                        else
                        {
                            holdTime = 0f;
                        }
                        
                        if (cycleForwardRelease && cycleModifierDown && holdTime.ApproximatelyEqualsTo(0f))
                        {
                            UpdateSettlementVisualEntity(true);
                        }
                        else if (cycleBackRelease && cycleModifierDown && holdTime.ApproximatelyEqualsTo(0f))
                        {
                            UpdateSettlementVisualEntity(false);
                        }
                    }
                    else
                    {
                        holdTime = 0f;
                    }



                    Vec3 worldMouseNear = Vec3.Zero;
                    Vec3 worldMouseFar = Vec3.Zero;
                    mapScreen.SceneLayer.SceneView.TranslateMouse(ref worldMouseNear, ref worldMouseFar);
                    Vec3 clippedMouseNear = worldMouseNear;
                    Vec3 clippedMouseFar = worldMouseFar;
                    PathFaceRecord currentFace = PathFaceRecord.NullFaceRecord;
                    mapScreen.GetCursorIntersectionPoint(ref clippedMouseNear, ref clippedMouseFar, out float closestDistanceSquared, out Vec3 _, ref currentFace);
                    mapScreen.GetCursorIntersectionPoint(ref clippedMouseNear, ref clippedMouseFar, out float _, out Vec3 intersectionPoint2, ref currentFace, BodyFlags.Disabled | BodyFlags.Moveable | BodyFlags.AILimiter | BodyFlags.Barrier | BodyFlags.Barrier3D | BodyFlags.Ragdoll | BodyFlags.RagdollLimiter | BodyFlags.DoNotCollideWithRaycast);
                    MatrixFrame identity = MatrixFrame.Identity;
                    identity.origin = intersectionPoint2;

                    var previous = settlementVisualEntity.GetFrame();

                    var rotateModifierDown = mapScreen.SceneLayer.Input.IsAltDown();
                    if (inputInformation.RotateLeftKeyDown && rotateModifierDown)
                    {
                        this.settlementRotationVelocity = inputInformation.Dt * 2f;
                    }
                    else if (inputInformation.RotateRightKeyDown && rotateModifierDown)
                    {
                        this.settlementRotationVelocity = inputInformation.Dt * -2f;
                    }
                    this.settlementRotationVelocity = this.settlementRotationVelocity + inputInformation.HorizontalCameraInput * 1.75f * inputInformation.Dt;
                    if (inputInformation.RightMouseButtonDown && rotateModifierDown)
                    {
                        this.settlementRotationVelocity += 0.01f * inputInformation.MouseSensitivity * inputInformation.MouseMoveX;

                        // Divide by 5 for actual settings
                        this.settlementRotationVelocity *= (Main.Settings.MouseRotationModifier / 5);
                    }
                    else if (rotateModifierDown && (inputInformation.RotateLeftKeyDown || inputInformation.RotateRightKeyDown))
                    {
                        // Divide by 5 for actual settings
                        this.settlementRotationVelocity *= (Main.Settings.KeyRotationModifier / 5);
                    }


                    var bearing = this.settlementRotationBearing + this.settlementRotationVelocity;
                    identity.rotation.RotateAboutUp(-bearing);
                    this.settlementRotationBearing = bearing;
                    this.settlementRotationVelocity = 0f;

                    settlementPlacementFrame = identity;
                    this.SetFrame(ref identity);

                    bool flag = Campaign.Current.MapSceneWrapper.AreFacesOnSameIsland(currentFace, MobileParty.MainParty.CurrentNavigationFace, ignoreDisabled: false);
                    mapScreen.SceneLayer.ActiveCursor = (flag ? CursorType.Default : CursorType.Disabled);

                }
            }
        }

        // // TODO: Might be useful to ensure reachable
        //public Vec3 GetVisualPosition()
        //{
        //    float single = 0f;
        //    Vec2 zero = Vec2.Zero;
        //    Vec2 vec2 = new Vec2(this.PartyBase.Position2D.x + zero.x, this.PartyBase.Position2D.y + zero.y);

        //    return new Vec3(vec2, single, -1f);
        //}

        private void SetFrame(ref MatrixFrame frame)
        {
            if (this.settlementVisualEntity != null && !this.settlementVisualEntity.GetFrame().NearlyEquals(frame, 1E-05f))
            {
                this.settlementVisualEntity.SetFrame(ref frame);
            }
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
                    Debug.PrintError(e.Message, e.StackTrace);
                }
            }
            //Reset();
        }

        public void Reset()
        {
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
            Campaign.Current.SetTimeControlModeLock(false);
            availableModels?.Clear();
            currentModelOptionIdx = -1;
            ClearEntities();
            settlementRotationBearing = 0f;
            settlementRotationVelocity = 0f;
            settlementPlacementFrame = null;
            applyPending = null;
            SettlementRequest = SettlementType.None;
        }

        private void ClearEntities()
        {

            if (settlementVisualEntity != null)
            {
                try
                {
                    try
                    {
                        MapScreen.VisualsOfEntities.Remove(settlementVisualEntity.Pointer);
                    }
                    catch (Exception e)
                    {
                        Debug.PrintError(e.Message, e.StackTrace);
                    }
                    foreach (GameEntity child in settlementVisualEntity.GetChildren().ToList())
                    {
                        try
                        {
                            MapScreen.VisualsOfEntities.Remove(child.Pointer);
                            child.Remove(112);
                        }
                        catch (Exception e)
                        {
                            Debug.PrintError(e.Message, e.StackTrace);
                        }
                    }
                    try
                    {
                        settlementVisualEntity.ClearEntityComponents(true, true, true);
                        settlementVisualEntity.ClearOnlyOwnComponents();
                        settlementVisualEntity.ClearComponents();
                    }
                    catch (Exception e)
                    {
                        Debug.PrintError(e.Message, e.StackTrace);
                    }
                    settlementVisualEntity.Remove(112);
                }
                catch (Exception e)
                {
                    Debug.PrintError(e.Message, e.StackTrace);
                }
            }
            settlementVisualEntity = null;
        }

        public void Tick(float delta)
        {
            if (Main.Settings != null && Main.Settings.Enabled && PlayerSettlementInfo.Instance != null)
            {
                if (SettlementRequest == SettlementType.Town)
                {
                    Reset();

                    BuildTown();
                    return;
                }
                else if (SettlementRequest == SettlementType.Village)
                {
                    Reset();

                    BuildVillage();
                    return;
                }
                else if (SettlementRequest == SettlementType.Castle)
                {
                    Reset();

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

                    Action<string, CultureObject> applyPlaced = (string settlementName, CultureObject culture) =>
                    {
                        if (currentModelOptionIdx < 0)
                        {
                            currentModelOptionIdx = new Random().Next(0, availableModels.Count);
                        }

                        if (PlayerSettlementInfo.Instance!.Castles == null)
                        {
                            PlayerSettlementInfo.Instance!.Castles = new();
                        }

                        var atPos = settlementPlacementFrame?.origin != null ? settlementPlacementFrame.Value.origin.AsVec2 : MobileParty.MainParty.Position2D;

                        var castleNumber = PlayerSettlementInfo.Instance!.Castles!.Count + 1;

                        // For now gate position is the same as the main position.
                        // TODO: Determine if gate position can be calculated with rotation and ensure it is a reachable terrain.
                        var gPos = atPos;

                        var item = availableModels[currentModelOptionIdx];

                        var node = item.ItemXML.CloneNode(true);
                        node.Attributes["posX"].Value = atPos.X.ToString();
                        node.Attributes["posY"].Value = atPos.Y.ToString();
                        node.Attributes["name"].Value = settlementName;
                        node.Attributes["owner"].Value = $"Faction.{Hero.MainHero.Clan.StringId}";
                        node.Attributes["culture"].Value = $"Culture.{culture.StringId}";

                        TextObject encyclopediaText = new TextObject("{=player_settlement_24}{SETTLEMENT_NAME} was founded by {HERO_NAME} of the {FACTION_TERM} on {BUILD_TIME}");
                        encyclopediaText.SetTextVariable("SETTLEMENT_NAME", PlayerSettlementItem.EncyclopediaLinkWithName(item.Id, new TextObject(settlementName)));
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

                        var castleItem = new PlayerSettlementItem
                        {
                            ItemXML = xml,
                            Identifier = castleNumber,
                            Type = (int) SettlementType.Castle,
                            SettlementName = settlementName,
                            RotationMat3 = settlementPlacementFrame?.rotation,
                            Version = Main.Version,
                            StringId = item.Id
                        };
                        PlayerSettlementInfo.Instance.Castles.Add(castleItem);

                        var doc = new XmlDocument();
                        doc.LoadXml(xml);
                        MBObjectManager.Instance.LoadXml(doc);

                        var castleSettlement = MBObjectManager.Instance.GetObject<Settlement>(castleItem.StringId);
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

                        _settlementCreated.Invoke(castleItem.Settlement);
                        SaveHandler.SaveLoad(!Main.Settings.CreateNewSave);
                    };

                    Action<string, CultureObject> apply = (string settlementName, CultureObject culture) =>
                    {
                        settlementPlacementFrame = null;

                        Action confirmAndApply = () =>
                        {
                            var createPlayerSettlementText = new TextObject("{=player_settlement_19}Build a Castle").ToString();
                            var confirm = new TextObject("{=player_settlement_18}Are you sure you want to build your castle here?");

                            InformationManager.ShowInquiry(new InquiryData(createPlayerSettlementText, confirm.ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                                () =>
                                {
                                    InformationManager.HideInquiry();
                                    applyPlaced(settlementName, culture);
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
                        };

                        Func<CultureSettlementTemplate, List<PlayerSettlementItemTemplate>> select = cst =>
                        {
                            var templates = new List<PlayerSettlementItemTemplate>();
                            try
                            {
                                var castles = cst.Castles;
                                for (int i = 0; i < castles; i++)
                                {
                                    var variant = i + 1;
                                    var nodes = cst.Document.SelectNodes($"descendant::Settlement[@template_type='Castle']");
                                    // [@template_variant='{variant}']
                                    var node = nodes.OfType<XmlNode>().FirstOrDefault(n => n.Attributes["template_variant"].Value == $"{variant}");
                                    var id = node.Attributes["id"].Value;
                                    templates.Add(new PlayerSettlementItemTemplate
                                    {
                                        Id = id,
                                        ItemXML = node,
                                        Type = (int) SettlementType.Castle,
                                        Variant = variant,
                                        Culture = cst.CultureId
                                    });
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.PrintError(e.Message, e.StackTrace);
                            }
                            return templates;
                        };

                        if (Main.Settings.SelectedCultureOnly && Main.Submodule!.CultureTemplates.ContainsKey(culture.StringId))
                        {
                            availableModels = Main.Submodule.CultureTemplates[culture.StringId].SelectMany(select).Where(FilterAvailableModels).ToList();
                            currentModelOptionIdx = -1;
                        }
                        else
                        {
                            availableModels = Main.Submodule!.CultureTemplates.Values.SelectMany(c => c.SelectMany(select)).Where(FilterAvailableModels).ToList();
                            currentModelOptionIdx = availableModels.FindIndex(a => a.Culture == culture.StringId) - 1;
                        }

                        if (!Main.Settings!.SettlementPlacement)
                        {
                            confirmAndApply();
                            return;
                        }

                        UpdateSettlementVisualEntity(true);

                        applyPending = () => confirmAndApply();
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
                        minSelectableOptionCount: 1,
                        affirmativeText: GameTexts.FindText("str_ok", null).ToString(),
                        negativeText: null,
                        affirmativeAction: (List<InquiryElement> args) =>
                        {
                            List<InquiryElement> source = args;
                            //InformationManager.HideInquiry();

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
                    Reset();
                    MapBarExtensionVM.Current?.OnRefresh();
                }, false, new Func<string, Tuple<bool, string>>(FactionHelper.IsKingdomNameApplicable), "", ""), true, false);
        }

        private void BuildVillage()
        {
            if (Main.Settings!.AutoDetermineVillageOwner)
            {
                BuildVillageFor(null);
                return;
            }

            var titleText = new TextObject("{=player_settlement_22}Choose village bound settlement");
            var descriptionText = new TextObject("{=player_settlement_23}Choose the settlement to which this village is bound");

            List<InquiryElement> inquiryElements1 = GetPotentialVillageBoundOwners().Where(s => s != null).Select(c => new InquiryElement(c, c!.Name.ToString(), new ImageIdentifier(CharacterCode.CreateFrom((c.IsTown || c.IsCastle ? c.Town.Governor ?? Hero.MainHero : Hero.MainHero).CharacterObject)), true, (c.EncyclopediaText ?? c.Name).ToString())).ToList();
            
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

                    Action<string, CultureObject, string> applyPlaced = (string settlementName, CultureObject culture, string villageType) =>
                    {
                        if (currentModelOptionIdx < 0)
                        {
                            currentModelOptionIdx = new Random().Next(0, availableModels.Count);
                        }

                        var atPos = settlementPlacementFrame?.origin != null ? settlementPlacementFrame.Value.origin.AsVec2 : MobileParty.MainParty.Position2D;

                        var item = availableModels[currentModelOptionIdx];

                        var node = item.ItemXML.CloneNode(true);
                        node.Attributes["id"].Value = item.Id;
                        node.Attributes["posX"].Value = atPos.X.ToString();
                        node.Attributes["posY"].Value = atPos.Y.ToString();
                        node.Attributes["name"].Value = settlementName;
                        node.Attributes["culture"].Value = $"Culture.{culture.StringId}";

                        var newNodeComponent = node.SelectSingleNode("descendant::Village");
                        newNodeComponent.Attributes["id"].Value = newNodeComponent.Attributes["id"].Value.Replace("{{OWNER_TYPE}}", bound.IsCastle ? "castle" : "town");
                        newNodeComponent.Attributes["village_type"].Value = $"VillageType.{villageType}";
                        newNodeComponent.Attributes["bound"].Value = $"Settlement.{bound.StringId}";

                        TextObject encyclopediaText = new TextObject("{=player_settlement_24}{SETTLEMENT_NAME} was founded by {HERO_NAME} of the {FACTION_TERM} on {BUILD_TIME}");
                        encyclopediaText.SetTextVariable("SETTLEMENT_NAME", PlayerSettlementItem.EncyclopediaLinkWithName(item.Id, new TextObject(settlementName)));
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

                        var villageItem = new PlayerSettlementItem
                        {
                            ItemXML = xml,
                            Identifier = villageNumber,
                            Type = (int) SettlementType.Village,
                            SettlementName = settlementName,
                            RotationMat3 = settlementPlacementFrame?.rotation,
                            Version = Main.Version,
                            StringId = item.Id
                        };

                        if (boundTarget == null)
                        {
                            if (PlayerSettlementInfo.Instance.PlayerVillages == null)
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
                        MBObjectManager.Instance.LoadXml(doc);

                        var villageSettlement = MBObjectManager.Instance.GetObject<Settlement>(villageItem.StringId);
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

                        _settlementCreated.Invoke(villageItem.Settlement);
                        SaveHandler.SaveLoad(!Main.Settings.CreateNewSave);
                    };

                    Action<string, CultureObject, string> apply = (string settlementName, CultureObject culture, string villageType) =>
                    {
                        settlementPlacementFrame = null;

                        Action confirmAndApply = () =>
                        {
                            var createPlayerSettlementText = new TextObject("{=player_settlement_13}Build a Village").ToString();
                            var confirm = new TextObject("{=player_settlement_14}Are you sure you want to build your village here?");

                            InformationManager.ShowInquiry(new InquiryData(createPlayerSettlementText, confirm.ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                                () =>
                                {
                                    InformationManager.HideInquiry();
                                    applyPlaced(settlementName, culture, villageType);
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
                        };


                        Func<CultureSettlementTemplate, List<PlayerSettlementItemTemplate>> select = cst =>
                        {
                            var templates = new List<PlayerSettlementItemTemplate>();
                            try
                            {
                                var villages = cst.Villages;
                                for (int i = 0; i < villages; i++)
                                {
                                    var variant = i + 1;
                                    var nodes = cst.Document.SelectNodes($"descendant::Settlement[@template_type='Village']");
                                    // [@template_variant='{variant}']
                                    var node = nodes.OfType<XmlNode>().FirstOrDefault(n => n.Attributes["template_variant"].Value == $"{variant}");
                                    var id = node.Attributes["id"].Value.Replace("{{OWNER_TYPE}}", bound.IsCastle ? "castle" : "town");
                                    templates.Add(new PlayerSettlementItemTemplate
                                    {
                                        Id = id,
                                        ItemXML = node,
                                        Type = (int) SettlementType.Village,
                                        Variant = variant,
                                        Culture = cst.CultureId
                                    });
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.PrintError(e.Message, e.StackTrace);
                            }
                            return templates;
                        };

                        if (Main.Settings.SelectedCultureOnly && Main.Submodule!.CultureTemplates.ContainsKey(culture.StringId))
                        {
                            availableModels = Main.Submodule.CultureTemplates[culture.StringId].SelectMany(select).Where(FilterAvailableModels).ToList();
                            currentModelOptionIdx = -1;
                        }
                        else
                        {
                            availableModels = Main.Submodule!.CultureTemplates.Values.SelectMany(c => c.SelectMany(select)).Where(FilterAvailableModels).ToList();
                            currentModelOptionIdx = availableModels.FindIndex(a => a.Culture == culture.StringId) - 1;
                        }

                        if (!Main.Settings!.SettlementPlacement)
                        {
                            confirmAndApply();
                            return;
                        }

                        UpdateSettlementVisualEntity(true);

                        applyPending = () => confirmAndApply();
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
                            minSelectableOptionCount: 1,
                            affirmativeText: GameTexts.FindText("str_ok", null).ToString(),
                            negativeText: null,
                            affirmativeAction: (List<InquiryElement> args) =>
                            {
                                List<InquiryElement> source = args;
                                //InformationManager.HideInquiry();

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
                    Reset();
                    MapBarExtensionVM.Current?.OnRefresh();
                }, false, new Func<string, Tuple<bool, string>>(FactionHelper.IsKingdomNameApplicable), "", ""), true, false);
        }

        private bool FilterAvailableModels(PlayerSettlementItemTemplate potentialSettlement)
        {
            if (PlayerSettlementInfo.Instance == null)
            {
                return false;
            }

            switch ((SettlementType) potentialSettlement.Type)
            {
                default:
                case SettlementType.None:
                    return false;
                case SettlementType.Town:
                    return !PlayerSettlementInfo.Instance.Towns.Any(t => t.Settlement?.StringId == potentialSettlement.Id);
                case SettlementType.Village:
                    return !((PlayerSettlementInfo.Instance.PlayerVillages?.Any(v => v.Settlement?.StringId == potentialSettlement.Id) ?? false) ||
                              PlayerSettlementInfo.Instance.Towns.SelectMany(t => t.Villages).Any(v => v.Settlement?.StringId == potentialSettlement.Id) ||
                              PlayerSettlementInfo.Instance.Castles.SelectMany(c => c.Villages).Any(v => v.Settlement?.StringId == potentialSettlement.Id));
                case SettlementType.Castle:
                    return !PlayerSettlementInfo.Instance.Castles.Any(c => c.Settlement?.StringId == potentialSettlement.Id);
            }
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
                    Action<string, CultureObject> applyPlaced = (string settlementName, CultureObject culture) =>
                    {
                        if (currentModelOptionIdx < 0)
                        {
                            currentModelOptionIdx = new Random().Next(0, availableModels.Count);
                        }

                        if (PlayerSettlementInfo.Instance!.Towns == null)
                        {
                            PlayerSettlementInfo.Instance!.Towns = new();
                        }

                        var atPos = settlementPlacementFrame?.origin != null ? settlementPlacementFrame.Value.origin.AsVec2 : MobileParty.MainParty.Position2D;

                        var townNumber = PlayerSettlementInfo.Instance!.Towns!.Count + 1;

                        // For now gate position is the same as the main position.
                        // TODO: Determine if gate position can be calculated with rotation and ensure it is a reachable terrain.
                        var gPos = atPos;

                        var item = availableModels[currentModelOptionIdx];

                        var node = item.ItemXML.CloneNode(true);
                        node.Attributes["posX"].Value = atPos.X.ToString();
                        node.Attributes["posY"].Value = atPos.Y.ToString();
                        node.Attributes["name"].Value = settlementName;
                        node.Attributes["owner"].Value = $"Faction.{Hero.MainHero.Clan.StringId}";
                        node.Attributes["culture"].Value = $"Culture.{culture.StringId}";

                        TextObject encyclopediaText = new TextObject("{=player_settlement_24}{SETTLEMENT_NAME} was founded by {HERO_NAME} of the {FACTION_TERM} on {BUILD_TIME}");
                        encyclopediaText.SetTextVariable("SETTLEMENT_NAME", PlayerSettlementItem.EncyclopediaLinkWithName(item.Id, new TextObject(settlementName)));
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

                        var townItem = new PlayerSettlementItem
                        {
                            ItemXML = xml,
                            Identifier = townNumber,
                            Type = (int) SettlementType.Town,
                            SettlementName = settlementName,
                            RotationMat3 = settlementPlacementFrame?.rotation,
                            Version = Main.Version,
                            StringId = item.Id
                        };
                        PlayerSettlementInfo.Instance.Towns.Add(townItem);

                        var doc = new XmlDocument();
                        doc.LoadXml(xml);
                        MBObjectManager.Instance.LoadXml(doc);

                        var townSettlement = MBObjectManager.Instance.GetObject<Settlement>(townItem.StringId);
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
                            BuildingHelper.ChangeDefaultBuilding(townSettlement.Town.Buildings.FirstOrDefault<Building>(), townSettlement.Town);
                        }

                        var campaignGameStarter = SandBoxManager.Instance.GameStarter;
                        var craftingCampaignBehavior = campaignGameStarter.CampaignBehaviors.FirstOrDefault<CampaignBehaviorBase>(b => b is CraftingCampaignBehavior) as CraftingCampaignBehavior;
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

                        _settlementCreated.Invoke(townItem.Settlement);

                        //Reset();
                        SaveHandler.SaveLoad(!Main.Settings.CreateNewSave);
                    };


                    Action<string, CultureObject> apply = (string settlementName, CultureObject culture) =>
                    {
                        settlementPlacementFrame = null;

                        Action confirmAndApply = () =>
                        {
                            var createPlayerSettlementText = new TextObject("{=player_settlement_04}Build a Town").ToString();
                            var confirm = new TextObject("{=player_settlement_05}Are you sure you want to build your town here?");

                            InformationManager.ShowInquiry(new InquiryData(createPlayerSettlementText, confirm.ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                                () =>
                                {
                                    InformationManager.HideInquiry();
                                    applyPlaced(settlementName, culture);
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
                        };


                        Func<CultureSettlementTemplate, List<PlayerSettlementItemTemplate>> select = cst =>
                        {
                            var templates = new List<PlayerSettlementItemTemplate>();
                            try
                            {
                                var towns = cst.Towns;
                                for (int i = 0; i < towns; i++)
                                {
                                    var variant = i + 1;
                                    var nodes = cst.Document.SelectNodes($"descendant::Settlement[@template_type='Town']");
                                    // [@template_variant='{variant}']
                                    var node = nodes.OfType<XmlNode>().FirstOrDefault(n => n.Attributes["template_variant"].Value == $"{variant}");
                                    var id = node.Attributes["id"].Value;
                                    templates.Add(new PlayerSettlementItemTemplate
                                    {
                                        Id = id,
                                        ItemXML = node,
                                        Type = (int) SettlementType.Town,
                                        Variant = variant,
                                        Culture = cst.CultureId
                                    });
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.PrintError(e.Message, e.StackTrace);
                            }
                            return templates;
                        };

                        if (Main.Settings.SelectedCultureOnly && Main.Submodule!.CultureTemplates.ContainsKey(culture.StringId))
                        {
                            availableModels = Main.Submodule.CultureTemplates[culture.StringId].SelectMany(select).Where(FilterAvailableModels).ToList();
                            currentModelOptionIdx = -1;
                        }
                        else
                        {
                            availableModels = Main.Submodule!.CultureTemplates.Values.SelectMany(c => c.SelectMany(select)).Where(FilterAvailableModels).ToList();
                            currentModelOptionIdx = availableModels.FindIndex(a => a.Culture == culture.StringId) - 1;
                        }

                        if (!Main.Settings!.SettlementPlacement)
                        {
                            confirmAndApply();
                            return;
                        }

                        UpdateSettlementVisualEntity(true);

                        applyPending = () => confirmAndApply();
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
                        minSelectableOptionCount: 1,
                        affirmativeText: GameTexts.FindText("str_ok", null).ToString(),
                        negativeText: null,
                        affirmativeAction: (List<InquiryElement> args) =>
                        {
                            List<InquiryElement> source = args;
                            //InformationManager.HideInquiry();

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
                    Reset();
                    MapBarExtensionVM.Current?.OnRefresh();
                }, false, new Func<string, Tuple<bool, string>>(FactionHelper.IsKingdomNameApplicable), "", ""), true, false);
        }

        private void UpdateSettlementVisualEntity(bool forward)
        {
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
            //settlementRotationBearing = 0f;
            //settlementRotationVelocity = 0f;
            settlementPlacementFrame = null;

            var template = availableModels[currentModelOptionIdx];

            Campaign.Current.MapSceneWrapper.AddNewEntityToMapScene(template.Id, MobileParty.MainParty.Position2D);
            var mapScene = ((MapScene) Campaign.Current.MapSceneWrapper).Scene;
            settlementVisualEntity = mapScene.GetCampaignEntityWithName(template.Id);
            settlementVisualEntity.AddBodyFlags(BodyFlags.DoNotCollideWithRaycast | BodyFlags.DontCollideWithCamera | BodyFlags.DontTransferToPhysicsEngine | BodyFlags.CommonCollisionExcludeFlagsForEditor);
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

        public IEnumerable<Settlement?> GetPotentialVillageBoundOwners()
        {
            if (PlayerSettlementInfo.Instance == null || Main.Settings == null || PlayerSettlementInfo.Instance.TotalVillages >= Main.Settings.HardMaxVillages)
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
            SetUniqueGameId(Campaign.Current, new object[] { newId });

            return (oldId, Campaign.Current.UniqueGameId);
        }

        public Settlement? CalculateVillageOwner(/*out PlayerSettlementItem? boundTarget*/)
        {
            if (PlayerSettlementInfo.Instance == null || Main.Settings == null || PlayerSettlementInfo.Instance.TotalVillages >= Main.Settings.HardMaxVillages)
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