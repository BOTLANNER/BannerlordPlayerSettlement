
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

        private const string CreateNewSave_Hint = "Create a new save when building. By default, the current active save will be overwritten instead.  [ Default: OFF ]";

        [SettingPropertyBool("Create new save on build", HintText = CreateNewSave_Hint, RequireRestart = false, Order = 0, IsToggle = false)]
        [SettingPropertyGroup("Saves", GroupOrder = 0)]
        public bool CreateNewSave { get; set; } = false;

        private const string HideButtonUntilReady_Hint = "Always hides the build town button until requirements are met.  [ Default: OFF ]";

        [SettingPropertyBool("Always Hide Until Ready", HintText = HideButtonUntilReady_Hint, RequireRestart = false, Order = 0, IsToggle = false)]
        [SettingPropertyGroup("User Interface", GroupOrder = 1)]
        public bool HideButtonUntilReady { get; set; } = false;

        private const string SettlementPlacement_Hint = "Allows choosing the position and rotation to place the settlement. When disabled will use the player party current position.  [ Default: ON ]";

        [SettingPropertyBool("Enable Settlement Placement", HintText = SettlementPlacement_Hint, RequireRestart = false, Order = 20, IsToggle = true)]
        [SettingPropertyGroup("Settlement Placement", GroupOrder = 2)]
        public bool SettlementPlacement { get; set; } = true;

        private const string MouseRotationModifier_Hint = @"Speed at which mouse movement rotates settlement. 
Settlement rotation applies when 'Alt' is held.  [ Default: 50% ]";

        [SettingPropertyFloatingInteger("Mouse Rotation Speed", 0.01f, 10f, "#0%", HintText = MouseRotationModifier_Hint, RequireRestart = false, Order = 21)]
        [SettingPropertyGroup("Settlement Placement")]
        public float MouseRotationModifier { get; set; } = 0.5f;

        private const string KeyRotationModifier_Hint = @"Speed at which rotation keys affect settlement when rotating. 
Default game rotation keys are 'Q' and 'E', unless remapped. 
Settlement rotation applies when 'Alt' is held. [ Default: 100% ]";

        [SettingPropertyFloatingInteger("Key Rotation Speed", 0.01f, 10f, "#0%", HintText = KeyRotationModifier_Hint, RequireRestart = false, Order = 22)]
        [SettingPropertyGroup("Settlement Placement")]
        public float KeyRotationModifier { get; set; } = 1f;

        private const string SelectedCultureOnly_Hint = @"Will limit settlement options to selected culture only. 
Otherwise will allow settlement options for all cultures. 
Cycle visually between options by holding 'Shift' and using rotation keys. 
Default game rotation keys are 'Q' and 'E', unless remapped.  [ Default: ON ]";

        [SettingPropertyBool("Selected Culture Only", HintText = SelectedCultureOnly_Hint, RequireRestart = false, Order = 23, IsToggle = false)]
        [SettingPropertyGroup("Settlement Placement")]
        public bool SelectedCultureOnly { get; set; } = true;

        private const string CycleSpeed_Hint = @"Speed at which settlements will visually cycle during placement while holding 'Shift' and a rotation key. 
Cycle visually between options by holding 'Shift' and using rotation keys. 
Default game rotation keys are 'Q' and 'E', unless remapped.  [ Default: 50% ]";

        [SettingPropertyFloatingInteger("Settlement Cycle Speed", 0.01f, 10f, "#0%", HintText = CycleSpeed_Hint, RequireRestart = false, Order = 24)]
        [SettingPropertyGroup("Settlement Placement")]
        public float CycleSpeed { get; set; } = 2f;

        private const string Enabled_Hint = "Enables Player Settlement mod and adds the option map screen.  [ Default: ON ]";

        [SettingPropertyBool("Enabled", HintText = Enabled_Hint, RequireRestart = true, Order = 0, IsToggle = true)]
        [SettingPropertyGroup("Player Settlements", GroupOrder = 3)]
        public bool Enabled { get; set; } = true;

        private const string RequireClanTier_Hint = "Requires clan to be specified tier before being allowed to create a settlement.  [ Default: ON ]";

        [SettingPropertyBool("Require Clan Tier", HintText = RequireClanTier_Hint, RequireRestart = false, Order = 1, IsToggle = false)]
        [SettingPropertyGroup("Player Settlements")]
        public bool RequireClanTier { get; set; } = true;

        private const string RequiredClanTier_Hint = "Specified tier required before being allowed to create a settlement.  [ Default: 4 ]";

        [SettingPropertyInteger("Required Clan Tier", 1, 6, HintText = RequiredClanTier_Hint, RequireRestart = false, Order = 2)]
        [SettingPropertyGroup("Player Settlements")]
        public int RequiredClanTier { get; set; } = 4;

        private const string RequireGold_Hint = "Requires a specified cost in local currency to build new town.  [ Default: ON ]";

        [SettingPropertyBool("Require Town Cost", HintText = RequireGold_Hint, RequireRestart = false, Order = 3, IsToggle = false)]
        [SettingPropertyGroup("Player Settlements")]
        public bool RequireGold { get; set; } = true;

        private const string RequiredGold_Hint = "Specified cost in local currency to build new town.  [ Default: 10 000 ]";

        [SettingPropertyInteger("Required Town Cost", 1, 1_000_000, HintText = RequiredGold_Hint, RequireRestart = false, Order = 4)]
        [SettingPropertyGroup("Player Settlements")]
        public int RequiredGold { get; set; } = 10_000;

        private const string InstantBuild_Hint = "Skip required build duration and instantly completes town construction.  [ Default: OFF ]";

        [SettingPropertyBool("Instant Build", HintText = InstantBuild_Hint, RequireRestart = false, Order = 5, IsToggle = false)]
        [SettingPropertyGroup("Player Settlements")]
        public bool InstantBuild { get; set; } = false;

        private const string BuildDurationDays_Hint = "Specified days before town is done being built.  [ Default: 7 ]";

        [SettingPropertyInteger("Build Duration Days", 1, 365, HintText = BuildDurationDays_Hint, RequireRestart = false, Order = 6)]
        [SettingPropertyGroup("Player Settlements")]
        public int BuildDurationDays { get; set; } = 7;

        private const string ForcePlayerCulture_Hint = "Will use the player culture for the town. By default when this is OFF, the town culture can be chosen.  [ Default: OFF ]";

        [SettingPropertyBool("Use Player Culture", HintText = ForcePlayerCulture_Hint, RequireRestart = false, Order = 7, IsToggle = false)]
        [SettingPropertyGroup("Player Settlements")]
        public bool ForcePlayerCulture { get; set; } = false;

        private const string RequireVillageGold_Hint = "Requires a specified cost in local currency to build new village.  [ Default: ON ]";

        [SettingPropertyBool("Require Village Cost", HintText = RequireVillageGold_Hint, RequireRestart = false, Order = 8, IsToggle = false)]
        [SettingPropertyGroup("Player Settlements")]
        public bool RequireVillageGold { get; set; } = true;

        private const string RequiredVillageGold_Hint = "Specified cost in local currency to build new village.  [ Default: 3 000 ]";

        [SettingPropertyInteger("Required Village Cost", 1, 1_000_000, HintText = RequiredVillageGold_Hint, RequireRestart = false, Order = 9)]
        [SettingPropertyGroup("Player Settlements")]
        public int RequiredVillageGold { get; set; } = 3_000;

        private const string AutoAllocateVillageType_Hint = "Will automatically determine the type of village, which determines its primary product. By default when this is OFF, the type can be chosen.  [ Default: OFF ]";

        [SettingPropertyBool("Auto Allocate Village Type", HintText = AutoAllocateVillageType_Hint, RequireRestart = false, Order = 10, IsToggle = false)]
        [SettingPropertyGroup("Player Settlements")]
        public bool AutoAllocateVillageType { get; set; } = false;

        private const string AutoDetermineVillageOwner_Hint = "Will automatically determine the bound town/castle for the village. By default when this is OFF, the bound settlement can be chosen.  [ Default: OFF ]";

        [SettingPropertyBool("Auto Determine Village Bound Settlement", HintText = AutoDetermineVillageOwner_Hint, RequireRestart = false, Order = 11, IsToggle = false)]
        [SettingPropertyGroup("Player Settlements")]
        public bool AutoDetermineVillageOwner { get; set; } = false;

        private const string RequireCastleGold_Hint = "Requires a specified cost in local currency to build new castle.  [ Default: ON ]";

        [SettingPropertyBool("Require Castle Cost", HintText = RequireCastleGold_Hint, RequireRestart = false, Order = 12, IsToggle = false)]
        [SettingPropertyGroup("Player Settlements")]
        public bool RequireCastleGold { get; set; } = true;

        private const string RequiredCastleGold_Hint = "Specified cost in local currency to build new castle.  [ Default: 10 000 ]";

        [SettingPropertyInteger("Required Castle Cost", 1, 1_000_000, HintText = RequiredCastleGold_Hint, RequireRestart = false, Order = 13)]
        [SettingPropertyGroup("Player Settlements")]
        public int RequiredCastleGold { get; set; } = 10_000;

        private const string MaxTowns_Hint = "Maximum number of player built towns allowed. At least one town is required.  [ Default: 10 ]";

        [SettingPropertyInteger("Maximum Allowed Towns", 1, HardMaxTowns, HintText = MaxTowns_Hint, RequireRestart = false, Order = 14)]
        [SettingPropertyGroup("Player Settlements")]
        public int MaxTowns { get; set; } = HardMaxTowns;

        private const string MaxVillagesPerTown_Hint = "Maximum number of player built villages per town allowed.  [ Default: 5 ]";

        [SettingPropertyInteger("Maximum Allowed Villages Per Town", 0, HardMaxVillagesPerTown, HintText = MaxVillagesPerTown_Hint, RequireRestart = false, Order = 15)]
        [SettingPropertyGroup("Player Settlements")]
        public int MaxVillagesPerTown { get; set; } = HardMaxVillagesPerTown;

        private const string MaxCastles_Hint = "Maximum number of player built castles allowed. At least one town is required first.  [ Default: 15 ]";

        [SettingPropertyInteger("Maximum Allowed Castles", 0, HardMaxCastles, HintText = MaxCastles_Hint, RequireRestart = false, Order = 16)]
        [SettingPropertyGroup("Player Settlements")]
        public int MaxCastles { get; set; } = HardMaxCastles;

        private const string MaxVillagesPerCastle_Hint = "Maximum number of player built villages per castle allowed.  [ Default: 4 ]";

        [SettingPropertyInteger("Maximum Allowed Villages Per Castle", 0, HardMaxVillagesPerCastle, HintText = MaxVillagesPerCastle_Hint, RequireRestart = false, Order = 17)]
        [SettingPropertyGroup("Player Settlements")]
        public int MaxVillagesPerCastle { get; set; } = HardMaxVillagesPerCastle;

//        private const string CastleChance_Hint = @"Percentage used to determine chance of next build being either a town or castle. 
//Higher percentage means castle is more likely until maximum runs out. 
//Maximum village count per town or castle has to be reached before next castle or town can be built.  [ Default: 50% ]";

//        [SettingPropertyInteger("Castle Chance", 0, 100, HintText = CastleChance_Hint, RequireRestart = false, Order = 18)]
//        [SettingPropertyGroup("Player Settlements")]
//        public int CastleChance { get; set; } = 50;

        private const string SingleConstruction_Hint = "Will require in progress construction to finish before being allowed to build next settlement. By default when this is OFF, multiple settlement construction can be done at once.  [ Default: OFF ]";

        [SettingPropertyBool("Single Construction At a Time", HintText = SingleConstruction_Hint, RequireRestart = false, Order = 19, IsToggle = false)]
        [SettingPropertyGroup("Player Settlements")]
        public bool SingleConstruction { get; set; } = false;

        // These numbers may only be increased after releases, never decreased as that WILL break backwards compatibility!
        public const int HardMaxTowns = 10;
        public const int HardMaxVillagesPerTown = 5;
               
        public const int HardMaxCastles = 15;
        public const int HardMaxVillagesPerCastle = 4;

        public readonly int HardMaxVillages = (HardMaxTowns * HardMaxVillagesPerTown) + (HardMaxCastles * HardMaxVillagesPerCastle);

    }
}
