using System.Collections.Generic;

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

        private const string Enabled_Hint = "Enables Player Settlement mod and adds the option map screen.  [ Default: ON ]";

        [SettingPropertyBool("Enabled", HintText = Enabled_Hint, RequireRestart = true, Order = 0, IsToggle = true)]
        [SettingPropertyGroup("Player Settlements", GroupOrder = 0)]
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

        private const string HideButtonUntilReady_Hint = "Always hides the build town button until requirements are met.  [ Default: OFF ]";

        [SettingPropertyBool("Always Hide Until Ready", HintText = HideButtonUntilReady_Hint, RequireRestart = false, Order = 0, IsToggle = false)]
        [SettingPropertyGroup("User Interface", GroupOrder = 1)]
        public bool HideButtonUntilReady { get; set; } = false;
    }
}
