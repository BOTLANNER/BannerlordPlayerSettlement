
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
#if MCM_v5
using MCM.Abstractions.Base.Global;
#else
using MCM.Abstractions.Settings.Base.Global;
#endif

namespace BannerlordPlayerSettlement
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => $"{Main.Name}_v1";
        public override string DisplayName => Main.DisplayName;
        public override string FolderName => Main.Name;
        public override string FormatType => "json";

        private const string CreateNewSave_Hint = "{=player_settlement_n_03}Create a new save when building. By default, the current active save will be overwritten instead.  [ Default: OFF ]";

        [SettingPropertyBool("{=player_settlement_n_04}Create new save on build", HintText = CreateNewSave_Hint, RequireRestart = false, Order = 0, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_66}Saves", GroupOrder = 0)]
        public bool CreateNewSave { get; set; } = false;

        private const string HideButtonUntilReady_Hint = @"{=player_settlement_n_05}Always hides the build panel until requirements are met for at least one build option. 
When using dialogue options, only available options will show, otherwise the unavailable ones will have hints describing why they are not available.  [ Default: OFF ]";

        [SettingPropertyBool("{=player_settlement_n_06}Always Hide Until Ready", HintText = HideButtonUntilReady_Hint, RequireRestart = false, Order = 0, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_67}User Interface", GroupOrder = 1)]
        public bool HideButtonUntilReady { get; set; } = false;

        private const string ImmersiveMode_Hint = @"{=player_settlement_n_07}Always hides the build panel. Building can only be started by discussing with a companion (if enabled).  [ Default: OFF ]";

        [SettingPropertyBool("{=player_settlement_n_08}Immersive Mode", HintText = ImmersiveMode_Hint, RequireRestart = false, Order = 1, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_67}User Interface")]
        public bool ImmersiveMode { get; set; } = false;

        private const string NoDialogue_Hint = @"{=player_settlement_n_09}Removes the build conversation options. Building can only be started using the build panel (if enabled).  [ Default: OFF ]";

        [SettingPropertyBool("{=player_settlement_n_10}No Conversation Options", HintText = NoDialogue_Hint, RequireRestart = false, Order = 1, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_67}User Interface")]
        public bool NoDialogue { get; set; } = false;

        private const string SettlementPlacement_Hint = "{=player_settlement_n_11}Allows choosing the position and rotation to place the settlement. When disabled will use the player party current position.  [ Default: ON ]";

        [SettingPropertyBool("{=player_settlement_n_12}Enable Settlement Placement", HintText = SettlementPlacement_Hint, RequireRestart = false, Order = 20, IsToggle = true)]
        [SettingPropertyGroup("{=player_settlement_n_70}Settlement Placement", GroupOrder = 2)]
        public bool SettlementPlacement { get; set; } = true;

        private const string MouseRotationModifier_Hint = @"{=player_settlement_n_13}Speed at which mouse movement rotates settlement. 
Settlement rotation applies when 'Alt' is held.  [ Default: 50% ]";

        [SettingPropertyFloatingInteger("{=player_settlement_n_14}Mouse Rotation Speed", 0.01f, 10f, "#0%", HintText = MouseRotationModifier_Hint, RequireRestart = false, Order = 21)]
        [SettingPropertyGroup("{=player_settlement_n_70}Settlement Placement")]
        public float MouseRotationModifier { get; set; } = 0.5f;

        private const string KeyRotationModifier_Hint = @"{=player_settlement_n_15}Speed at which rotation keys affect settlement when rotating. 
Default game rotation keys are 'Q' and 'E', unless remapped. 
Settlement rotation applies when 'Alt' is held. [ Default: 100% ]";

        [SettingPropertyFloatingInteger("{=player_settlement_n_16}Key Rotation Speed", 0.01f, 10f, "#0%", HintText = KeyRotationModifier_Hint, RequireRestart = false, Order = 22)]
        [SettingPropertyGroup("{=player_settlement_n_70}Settlement Placement")]
        public float KeyRotationModifier { get; set; } = 1f;

        private const string SelectedCultureOnly_Hint = @"{=player_settlement_n_17}Will limit settlement options to selected culture only. 
Otherwise will allow settlement options for all cultures. 
Cycle visually between options by holding 'Shift' and using cycle keys. 
Default game cycle keys are 'Q' and 'E', unless remapped.  [ Default: ON ]";

        [SettingPropertyBool("{=player_settlement_n_18}Selected Culture Only", HintText = SelectedCultureOnly_Hint, RequireRestart = false, Order = 23, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_70}Settlement Placement")]
        public bool SelectedCultureOnly { get; set; } = true;

        private const string CycleSpeed_Hint = @"{=player_settlement_n_19}Speed at which settlements will visually cycle during placement while holding 'Shift' and a cycle key, or speed at which settlements will scale while holding 'Ctrl' and a scale key.
Cycle visually between options by holding 'Shift' and using cycle keys. 
Scale by holding 'Ctrl' and using scale keys. 
Default cycle and scale keys are 'Q' and 'E', unless remapped.  [ Default: 50% ]";

        [SettingPropertyFloatingInteger("{=player_settlement_n_20}Settlement Cycle Speed", 0.01f, 10f, "#0%", HintText = CycleSpeed_Hint, RequireRestart = false, Order = 24)]
        [SettingPropertyGroup("{=player_settlement_n_70}Settlement Placement")]
        public float CycleSpeed { get; set; } = 2f;

        private const string AllowGatePosition_Hint = @"{=player_settlement_n_68}Allow choosing settlement gate position when applicable. When not enabled, all settlement entry is at the center.  [ Default: ON ]";

        [SettingPropertyBool("{=player_settlement_n_69}Allow Setting Gate Position", HintText = AllowGatePosition_Hint, RequireRestart = false, Order = 25, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_70}Settlement Placement")]
        public bool AllowGatePosition { get; set; } = true;

        private const string DisableAutoHints_Hint = @"{=player_settlement_n_71}Will disable automatic hints shown during settlement placement. Hints can still be shown by pressing the help key, by default 'F1' unless remapped.  [ Default: OFF ]";

        [SettingPropertyBool("{=player_settlement_n_72}Disable Automatic Hints", HintText = DisableAutoHints_Hint, RequireRestart = false, Order = 26, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_70}Settlement Placement")]
        public bool DisableAutoHints { get; set; } = false;

        private const string HintDurationSeconds_Hint = @"{=player_settlement_n_117}Duration to display hints (in seconds). This is in addition to the base time that the game applies by default. [ Default: 3 ]";

        [SettingPropertyInteger("{=player_settlement_n_116}Hint Duration", 1, 120, HintText = HintDurationSeconds_Hint, RequireRestart = false, Order = 27)]
        [SettingPropertyGroup("{=player_settlement_n_70}Settlement Placement")]
        public int HintDurationSeconds { get; set; } = 3;

        private const string Enabled_Hint = "{=player_settlement_n_21}Enables Player Settlement mod and adds the option map screen.  [ Default: ON ]";

        [SettingPropertyBool("{=player_settlement_n_64}Enabled", HintText = Enabled_Hint, RequireRestart = true, Order = 0, IsToggle = true)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements", GroupOrder = 3)]
        public bool Enabled { get; set; } = true;

        private const string RequireClanTier_Hint = "{=player_settlement_n_22}Requires clan to be specified tier before being allowed to create a settlement.  [ Default: ON ]";

        [SettingPropertyBool("{=player_settlement_n_23}Require Clan Tier", HintText = RequireClanTier_Hint, RequireRestart = false, Order = 1, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public bool RequireClanTier { get; set; } = true;

        private const string RequiredClanTier_Hint = "{=player_settlement_n_24}Specified tier required before being allowed to create a settlement.  [ Default: 4 ]";

        [SettingPropertyInteger("{=player_settlement_n_25}Required Clan Tier", 1, 6, HintText = RequiredClanTier_Hint, RequireRestart = false, Order = 2)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int RequiredClanTier { get; set; } = 4;

        private const string RequireGold_Hint = "{=player_settlement_n_26}Requires a specified cost in local currency to build new town.  [ Default: ON ]";

        [SettingPropertyBool("{=player_settlement_n_27}Require Town Cost", HintText = RequireGold_Hint, RequireRestart = false, Order = 3, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public bool RequireGold { get; set; } = true;

        private const string RequiredGold_Hint = "{=player_settlement_n_28}Specified cost in local currency to build new town.  [ Default: 10 000 ]";

        [SettingPropertyInteger("{=player_settlement_n_29}Required Town Cost", 1, 100_000_000, HintText = RequiredGold_Hint, RequireRestart = false, Order = 4)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int RequiredGold { get; set; } = 10_000;

        private const string RebuildTownRequiredGold_Hint = "{=player_settlement_n_124}Specified cost in local currency to rebuild a town.  [ Default: 5 000 ]";

        [SettingPropertyInteger("{=player_settlement_n_125}Required Town Rebuild Cost", 1, 100_000_000, HintText = RebuildTownRequiredGold_Hint, RequireRestart = false, Order = 4)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int RebuildTownRequiredGold { get; set; } = 5_000;

        private const string InstantBuild_Hint = "{=player_settlement_n_30}Skip required build duration and instantly completes town construction.  [ Default: OFF ]";

        [SettingPropertyBool("{=player_settlement_n_31}Instant Build", HintText = InstantBuild_Hint, RequireRestart = false, Order = 5, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public bool InstantBuild { get; set; } = false;

        private const string BuildDurationDays_Hint = "{=player_settlement_n_32}Specified days before town is done being built.  [ Default: 7 ]";

        [SettingPropertyInteger("{=player_settlement_n_113}Build Duration Days (Town)", 1, 2480, HintText = BuildDurationDays_Hint, RequireRestart = false, Order = 6)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int BuildDurationDays { get; set; } = 7;

        private const string BuildCastleDurationDays_Hint = "{=player_settlement_n_111}Specified days before castle is done being built.  [ Default: 7 ]";

        [SettingPropertyInteger("{=player_settlement_n_114}Build Duration Days (Castle)", 1, 2480, HintText = BuildCastleDurationDays_Hint, RequireRestart = false, Order = 6)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int BuildCastleDurationDays { get; set; } = 7;

        private const string BuildVillageDurationDays_Hint = "{=player_settlement_n_112}Specified days before village is done being built.  [ Default: 3 ]";

        [SettingPropertyInteger("{=player_settlement_n_115}Build Duration Days (Village)", 1, 2480, HintText = BuildVillageDurationDays_Hint, RequireRestart = false, Order = 6)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int BuildVillageDurationDays { get; set; } = 3;

        private const string RebuildTownDurationDays_Hint = "{=player_settlement_n_118}Specified days before town is done being rebuilt.  [ Default: 5 ]";

        [SettingPropertyInteger("{=player_settlement_n_121}Rebuild Duration Days (Town)", 1, 2480, HintText = RebuildTownDurationDays_Hint, RequireRestart = false, Order = 6)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int RebuildTownDurationDays { get; set; } = 5;

        private const string RebuildCastleDurationDays_Hint = "{=player_settlement_n_119}Specified days before castle is done being rebuilt.  [ Default: 4 ]";

        [SettingPropertyInteger("{=player_settlement_n_122}Rebuild Duration Days (Castle)", 1, 2480, HintText = RebuildCastleDurationDays_Hint, RequireRestart = false, Order = 6)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int RebuildCastleDurationDays { get; set; } = 7;

        private const string RebuildVillageDurationDays_Hint = "{=player_settlement_n_120}Specified days before village is done being rebuilt.  [ Default: 2 ]";

        [SettingPropertyInteger("{=player_settlement_n_123}Rebuild Duration Days (Village)", 1, 2480, HintText = RebuildVillageDurationDays_Hint, RequireRestart = false, Order = 6)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int RebuildVillageDurationDays { get; set; } = 2;

        private const string ForcePlayerCulture_Hint = "{=player_settlement_n_34}Will use the player culture for the town. By default when this is OFF, the town culture can be chosen.  [ Default: OFF ]";

        [SettingPropertyBool("{=player_settlement_n_35}Use Player Culture", HintText = ForcePlayerCulture_Hint, RequireRestart = false, Order = 7, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public bool ForcePlayerCulture { get; set; } = false;

        private const string RequireVillageGold_Hint = "{=player_settlement_n_36}Requires a specified cost in local currency to build new village.  [ Default: ON ]";

        [SettingPropertyBool("{=player_settlement_n_37}Require Village Cost", HintText = RequireVillageGold_Hint, RequireRestart = false, Order = 8, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public bool RequireVillageGold { get; set; } = true;

        private const string RequiredVillageGold_Hint = "{=player_settlement_n_38}Specified cost in local currency to build new village.  [ Default: 3 000 ]";

        [SettingPropertyInteger("{=player_settlement_n_39}Required Village Cost", 1, 100_000_000, HintText = RequiredVillageGold_Hint, RequireRestart = false, Order = 9)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int RequiredVillageGold { get; set; } = 3_000;

        private const string RebuildVillageRequiredGold_Hint = "{=player_settlement_n_126}Specified cost in local currency to rebuild a village.  [ Default: 1 000 ]";

        [SettingPropertyInteger("{=player_settlement_n_127}Required Village Rebuild Cost", 1, 100_000_000, HintText = RebuildVillageRequiredGold_Hint, RequireRestart = false, Order = 9)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int RebuildVillageRequiredGold { get; set; } = 1_000;

        private const string AutoAllocateVillageType_Hint = "{=player_settlement_n_40}Will automatically determine the type of village, which determines its primary product. By default when this is OFF, the type can be chosen.  [ Default: OFF ]";

        [SettingPropertyBool("{=player_settlement_n_41}Auto Allocate Village Type", HintText = AutoAllocateVillageType_Hint, RequireRestart = false, Order = 10, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public bool AutoAllocateVillageType { get; set; } = false;

        private const string AutoDetermineVillageOwner_Hint = "{=player_settlement_n_42}Will automatically determine the bound town/castle for the village. By default when this is OFF, the bound settlement can be chosen.  [ Default: OFF ]";

        [SettingPropertyBool("{=player_settlement_n_43}Auto Determine Village Bound Settlement", HintText = AutoDetermineVillageOwner_Hint, RequireRestart = false, Order = 11, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public bool AutoDetermineVillageOwner { get; set; } = false;

        private const string RequireCastleGold_Hint = "{=player_settlement_n_44}Requires a specified cost in local currency to build new castle.  [ Default: ON ]";

        [SettingPropertyBool("{=player_settlement_n_45}Require Castle Cost", HintText = RequireCastleGold_Hint, RequireRestart = false, Order = 12, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public bool RequireCastleGold { get; set; } = true;

        private const string RequiredCastleGold_Hint = "{=player_settlement_n_46}Specified cost in local currency to build new castle.  [ Default: 7 500 ]";

        [SettingPropertyInteger("{=player_settlement_n_47}Required Castle Cost", 1, 100_000_000, HintText = RequiredCastleGold_Hint, RequireRestart = false, Order = 13)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int RequiredCastleGold { get; set; } = 7_500;

        private const string RebuildCastleRequiredGold_Hint = "{=player_settlement_n_128}Specified cost in local currency to rebuild a castle.  [ Default: 3 500 ]";

        [SettingPropertyInteger("{=player_settlement_n_129}Required Castle Rebuild Cost", 1, 100_000_000, HintText = RebuildCastleRequiredGold_Hint, RequireRestart = false, Order = 13)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int RebuildCastleRequiredGold { get; set; } = 3_500;

        private const string MaxTowns_Hint = "{=player_settlement_n_48}Maximum number of player built towns allowed. At least one town is required.  [ Default: 10 ]";

        [SettingPropertyInteger("{=player_settlement_n_49}Maximum Allowed Towns", 1, HardMaxTowns, HintText = MaxTowns_Hint, RequireRestart = false, Order = 14)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int MaxTowns { get; set; } = HardMaxTowns;

        private const string MaxVillagesPerTown_Hint = "{=player_settlement_n_50}Maximum number of player built villages per town allowed.  [ Default: 5 ]";

        [SettingPropertyInteger("{=player_settlement_n_51}Maximum Allowed Villages Per Town", 0, HardMaxVillagesPerTown, HintText = MaxVillagesPerTown_Hint, RequireRestart = false, Order = 15)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int MaxVillagesPerTown { get; set; } = HardMaxVillagesPerTown;

        private const string MaxCastles_Hint = "{=player_settlement_n_52}Maximum number of player built castles allowed. At least one town is required first.  [ Default: 15 ]";

        [SettingPropertyInteger("{=player_settlement_n_53}Maximum Allowed Castles", 0, HardMaxCastles, HintText = MaxCastles_Hint, RequireRestart = false, Order = 16)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int MaxCastles { get; set; } = HardMaxCastles;

        private const string MaxVillagesPerCastle_Hint = "{=player_settlement_n_54}Maximum number of player built villages per castle allowed.  [ Default: 4 ]";

        [SettingPropertyInteger("{=player_settlement_n_55}Maximum Allowed Villages Per Castle", 0, HardMaxVillagesPerCastle, HintText = MaxVillagesPerCastle_Hint, RequireRestart = false, Order = 17)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public int MaxVillagesPerCastle { get; set; } = HardMaxVillagesPerCastle;

        private const string SingleConstruction_Hint = "{=player_settlement_n_56}Will require in progress construction to finish before being allowed to build next settlement. By default when this is OFF, multiple settlement construction can be done at once.  [ Default: OFF ]";

        [SettingPropertyBool("{=player_settlement_n_57}Single Construction At a Time", HintText = SingleConstruction_Hint, RequireRestart = false, Order = 19, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public bool SingleConstruction { get; set; } = false;

        private const string AddInitialGarrison_Hint = "{=player_settlement_n_58}Will add an initial garrison for new towns and castles.  [ Default: ON ]";

        [SettingPropertyBool("{=player_settlement_n_59}Add Initial Garrison", HintText = AddInitialGarrison_Hint, RequireRestart = false, Order = 20, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public bool AddInitialGarrison { get; set; } = true;

        private const string AddInitialMilitia_Hint = "{=player_settlement_n_60}Will add initial militia for new settlements.  [ Default: ON ]";

        [SettingPropertyBool("{=player_settlement_n_61}Add Initial Militia", HintText = AddInitialMilitia_Hint, RequireRestart = false, Order = 21, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public bool AddInitialMilitia { get; set; } = true;

        private const string AddInitialNotables_Hint = "{=player_settlement_n_62}Will add initial notables for new towns and villages.  [ Default: ON ]";

        [SettingPropertyBool("{=player_settlement_n_63}Add Initial Notables", HintText = AddInitialNotables_Hint, RequireRestart = false, Order = 22, IsToggle = false)]
        [SettingPropertyGroup("{=player_settlement_n_65}Player Settlements")]
        public bool AddInitialNotables { get; set; } = true;

        // These numbers may only be increased after releases, never decreased as that WILL break backwards compatibility!
        public const int HardMaxTowns = 150;
        public const int HardMaxVillagesPerTown = 50;
               
        public const int HardMaxCastles = 150;
        public const int HardMaxVillagesPerCastle = 50;

        public const int HardMaxVillages = int.MaxValue; //(HardMaxTowns * HardMaxVillagesPerTown) + (HardMaxCastles * HardMaxVillagesPerCastle);

    }
}
