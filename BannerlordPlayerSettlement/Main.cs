using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

using Bannerlord.ButterLib.HotKeys;
using Bannerlord.UIExtenderEx;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Descriptors;
using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.HotKeys;
using BannerlordPlayerSettlement.Patches.Compatibility.Interfaces;
using BannerlordPlayerSettlement.Saves;
using BannerlordPlayerSettlement.UI.Viewmodels;
using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

using Debug = TaleWorlds.Library.Debug;
using InputKey = TaleWorlds.InputSystem.InputKey;

namespace BannerlordPlayerSettlement
{
    public class Main : MBSubModuleBase
    {
        public static readonly string Version = $"{typeof(Main).Assembly.GetName().Version}";

        /**
         * Version with which StringId was introduced as opposed to a built up Identifier based on count
         */
        public static readonly Version Feature_StringId_Version = new("4.0.0.0");

        /**
         * Version with which PrefabId was introduced for prefabs instead of tied to StringId
         */
        public static readonly Version Feature_PrefabId_Version = new("5.0.0.0");

        /**
         * Version with which component was fixed to be unique identifier to prevent towns/villages from becoming one another
         */
        public static readonly Version Feature_Component_Fix_Version = new("5.1.0.0");

        public static readonly string Name = typeof(Main).Namespace;
        public static readonly string DisplayName = "Player Settlement"; // to be shown to humans in-game
        public static readonly string HarmonyDomain = "com.b0tlanner.bannerlord." + Name.ToLower();
        public static readonly string ModuleName = "PlayerSettlement";

        public static readonly string DefaultCategory = ModuleName;
        public static readonly string CycleCategory = ModuleName + "Cycle";
        public static readonly string RotateCategory = ModuleName + "Rotate";
        public static readonly string ScaleCategory = ModuleName + "Scale";

        public static readonly string DeleteCategory = ModuleName + "Delete";

        public static Settings? Settings;

        private bool _loaded;
        public static Harmony? Harmony;
        private UIExtender? _extender;

        public static Main? Submodule = null;

        private static List<ICompatibilityPatch> HarmonyCompatPatches = LoadCompatPatches().ToList();

        public Dictionary<string, List<CultureSettlementTemplate>> CultureTemplates;

        private string? _blacklistFile;

        private ModifierKey helpKey;
        public HotKeyBase HelpKey => helpKey;

        private ModifierKey cycleModifierKey;
        public HotKeyBase CycleModifierKey => cycleModifierKey;

        private ModifierKey rotateModifierKey;
        public HotKeyBase RotateModifierKey => rotateModifierKey;

        private ModifierKey rotateAlternateModifierKey;
        public HotKeyBase RotateAlternateModifierKey => rotateAlternateModifierKey;

        private ModifierKey scaleModifierKey;
        public HotKeyBase ScaleModifierKey => scaleModifierKey;

        private ModifierKey deepEditToggleKey;
        public HotKeyBase DeepEditToggleKey => deepEditToggleKey;

        private ModifierKey deepEditApplyKey;
        public HotKeyBase DeepEditApplyKey => deepEditApplyKey;

        private ModifierKey cycleBackKey;
        public HotKeyBase CycleBackKey => cycleBackKey;

        private ModifierKey cycleNextKey;
        public HotKeyBase CycleNextKey => cycleNextKey;

        private ModifierKey moveUpKey;
        public HotKeyBase MoveUpKey => moveUpKey;

        private ModifierKey moveDownKey;
        public HotKeyBase MoveDownKey => moveDownKey;

        private ModifierKey scaleSmallerKey;
        public HotKeyBase ScaleSmallerKey => scaleSmallerKey;

        private ModifierKey scaleBiggerKey;
        public HotKeyBase ScaleBiggerKey => scaleBiggerKey;

        private ModifierKey rotatePreviousKey;
        public HotKeyBase RotatePreviousKey => rotatePreviousKey;

        private ModifierKey rotateNextKey;
        public HotKeyBase RotateNextKey => rotateNextKey;

        private ModifierKey rotateBackwardsKey;
        public HotKeyBase RotateBackwardsKey => rotateBackwardsKey;

        private ModifierKey rotateForwardKey;
        public HotKeyBase RotateForwardKey => rotateForwardKey;

        private BasicHotKey deleteKey;
        public HotKeyBase DeleteKey => deleteKey;

        private BasicHotKey unDeleteModifierKey;
        public HotKeyBase UnDeleteModifierKey => unDeleteModifierKey;

        public string? BlacklistFile => _blacklistFile;
        public static MBReadOnlyList<string?> BlacklistedTemplates => _blacklistedTemplates;

        private static readonly MBList<string?> _blacklistedTemplates = new();

        public Main()
        {
            //Ctor
            Submodule = this;
        }

        protected override void OnSubModuleLoad()
        {
            try
            {
                base.OnSubModuleLoad();
                LogManager.EnableTracer = true; // enable code event tracing

                Harmony = new Harmony(HarmonyDomain);
                Harmony.PatchAll();

                foreach (var patch in HarmonyCompatPatches)
                {
                    patch.PatchSubmoduleLoad(Harmony);
                }

                _extender = UIExtender.Create(ModuleName); //HarmonyDomain);
                _extender.Register(typeof(Main).Assembly);
                _extender.Enable();

                LogManager.EventTracer.Trace();
            }
            catch (System.Exception e)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                LogManager.Log.NotifyBad(e);
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            try
            {

                if (Settings.Instance is not null && Settings.Instance != Settings)
                {
                    Settings = Settings.Instance;

                    // register for settings property-changed events
                    Settings.PropertyChanged += Settings_OnPropertyChanged;
                    LogManager.EventTracer.Trace();
                }

                if (!_loaded)
                {
                    LogManager.Log.Print($"Loaded {DisplayName}", Colours.ImportantTextColor);
                    _loaded = true;

                    foreach (var patch in HarmonyCompatPatches)
                    {
                        patch.PatchAfterMenus(Harmony!);
                    }

                    CultureTemplates = GatherTemplates();

                    var categoryName = TaleWorlds.MountAndBlade.Module.CurrentModule.GlobalTextManager.GetGameText("str_key_category_name");

                    var defaultHotKeyManager = HotKeyManager.CreateWithOwnCategory(DefaultCategory, DefaultCategory);
                    if (defaultHotKeyManager != null)
                    {
                        TextObject description = new TextObject("{=player_settlement_n_85}Player Settlements");
                        categoryName.AddVariationWithId(DefaultCategory, description, new List<GameTextManager.ChoiceTag>());

                        helpKey = defaultHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_73}Show Help",
                                "{=player_settlement_n_74}During player settlement placement when building, will show contextual help info.",
                                TaleWorlds.InputSystem.InputKey.F1,
                                DefaultCategory
                            )
                        );
                        cycleModifierKey = defaultHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_75}Cycle Mode",
                                "{=player_settlement_n_76}During player settlement placement when building, will switch to cycle mode when held.",
                                TaleWorlds.InputSystem.InputKey.LeftShift,
                                DefaultCategory
                            )
                        );
                        rotateModifierKey = defaultHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_77}Rotation Mode",
                                "{=player_settlement_n_78}During player settlement placement when building, will switch to rotation mode when held.",
                                TaleWorlds.InputSystem.InputKey.LeftAlt,
                                DefaultCategory
                            )
                        );
                        scaleModifierKey = defaultHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_79}Scale Mode",
                                "{=player_settlement_n_80}During player settlement placement when building, will switch to scaling mode when held.",
                                TaleWorlds.InputSystem.InputKey.LeftControl,
                                DefaultCategory
                            )
                        );
                        deepEditToggleKey = defaultHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_81}Deep Edit Toggle",
                                "{=player_settlement_n_82}During player settlement placement when building, will toggle between placement and deep edit modes.",
                                TaleWorlds.InputSystem.InputKey.Tab,
                                DefaultCategory
                            )
                        );
                        deepEditApplyKey = defaultHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_83}Deep Edit Finish",
                                "{=player_settlement_n_84}During player settlement placement when building in deep edit mode, will finish and apply either to gate position selection when applicable or confirmation to finalise",
                                TaleWorlds.InputSystem.InputKey.Space,
                                DefaultCategory
                            )
                        );
                    }

                    var cycleHotKeyManager = HotKeyManager.CreateWithOwnCategory(CycleCategory, CycleCategory);
                    if (cycleHotKeyManager != null)
                    {
                        TextObject description = new TextObject("{=player_settlement_n_86}Player Settlements: Cycle Mode");
                        categoryName.AddVariationWithId(CycleCategory, description, new List<GameTextManager.ChoiceTag>());

                        cycleBackKey = cycleHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_87}Cycle Back",
                                "{=player_settlement_n_88}During player settlement placement when building, will cycle to the previous settlement model (or previous submodel when in deep edit mode).",
                                InputKey.Q,
                                CycleCategory
                            )
                        );
                        cycleNextKey = cycleHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_89}Cycle Next",
                                "{=player_settlement_n_90}During player settlement placement when building, will cycle to the next settlement model (or next submodel when in deep edit mode).",
                                InputKey.E,
                                CycleCategory
                            )
                        );
                        moveDownKey = cycleHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_91}Move Down",
                                "{=player_settlement_n_92}During player settlement placement when building, will move the selected object down.",
                                InputKey.S,
                                CycleCategory
                            )
                        );
                        moveUpKey = cycleHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_93}Move Up",
                                "{=player_settlement_n_94}During player settlement placement when building, will move the selected object up.",
                                InputKey.W,
                                CycleCategory
                            )
                        );
                    }

                    var scaleHotKeyManager = HotKeyManager.CreateWithOwnCategory(ScaleCategory, ScaleCategory);
                    if (scaleHotKeyManager != null)
                    {
                        TextObject description = new TextObject("{=player_settlement_n_95}Player Settlements: Scale Mode");
                        categoryName.AddVariationWithId(ScaleCategory, description, new List<GameTextManager.ChoiceTag>());

                        scaleSmallerKey = scaleHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_96}Scale Down",
                                "{=player_settlement_n_97}During player settlement placement when building, will scale down to the model.",
                                TaleWorlds.InputSystem.InputKey.Q,
                                ScaleCategory
                            )
                        );
                        scaleBiggerKey = scaleHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_98}Scale Up",
                                "{=player_settlement_n_99}During player settlement placement when building, will scale up to the model.",
                                TaleWorlds.InputSystem.InputKey.E,
                                ScaleCategory
                            )
                        );
                    }

                    var rotateHotKeyManager = HotKeyManager.CreateWithOwnCategory(RotateCategory, RotateCategory);
                    if (rotateHotKeyManager != null)
                    {
                        TextObject description = new TextObject("{=player_settlement_n_100}Player Settlements: Rotate Mode");
                        categoryName.AddVariationWithId(RotateCategory, description, new List<GameTextManager.ChoiceTag>());

                        rotatePreviousKey = rotateHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_101}Rotate Left",
                                "{=player_settlement_n_102}During player settlement placement when building, will rotate the model to the left.",
                                TaleWorlds.InputSystem.InputKey.Q,
                                RotateCategory
                            )
                        );
                        rotateNextKey = rotateHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_103}Rotate Right",
                                "{=player_settlement_n_104}During player settlement placement when building, will rotate the model to the right.",
                                TaleWorlds.InputSystem.InputKey.E,
                                RotateCategory
                            )
                        );
                        rotateBackwardsKey = rotateHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_105}Rotate Back",
                                "{=player_settlement_n_106}During player settlement placement when building, will rotate the model backwards.",
                                TaleWorlds.InputSystem.InputKey.S,
                                RotateCategory
                            )
                        );
                        rotateForwardKey = rotateHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_107}Rotate Forward",
                                "{=player_settlement_n_108}During player settlement placement when building, will rotate the model forwards.",
                                TaleWorlds.InputSystem.InputKey.W,
                                RotateCategory
                            )
                        );
                        rotateAlternateModifierKey = rotateHotKeyManager.Add
                        (
                            new ModifierKey
                            (
                                "{=player_settlement_n_109}Alternate Rotation Axis",
                                "{=player_settlement_n_110}During player settlement placement when building, will switch to alternate rotation mode when held together with the 'Rotation Modifier'. This changes the axis of rotation when using forwards and backwards rotation keys.",
                                TaleWorlds.InputSystem.InputKey.LeftControl,
                                RotateCategory
                            )
                        );
                    }

                    var deleteHotKeyManager = HotKeyManager.CreateWithOwnCategory(DeleteCategory, DeleteCategory);
                    if (deleteHotKeyManager != null)
                    {
                        TextObject description = new TextObject("{=player_settlement_n_130}Player Settlements: Deep Edit Mode");
                        categoryName.AddVariationWithId(DeleteCategory, description, new List<GameTextManager.ChoiceTag>());

                        deleteKey = deleteHotKeyManager.Add
                        (
                            new BasicHotKey
                            (
                                "{=player_settlement_n_131}Delete Selection",
                                "{=player_settlement_n_132}During player settlement placement when building in deep edit mode, will delete the selected model.",
                                TaleWorlds.InputSystem.InputKey.BackSpace,
                                DeleteCategory,
                                "delete"
                            )
                        );
                        unDeleteModifierKey = deleteHotKeyManager.Add
                        (
                            new BasicHotKey
                            (
                                "{=player_settlement_n_133}Un-Delete Mode",
                                "{=player_settlement_n_134}During player settlement placement when building in deep edit mode, when held, the 'Delete Selection' button will instead undo the previous delete.",
                                TaleWorlds.InputSystem.InputKey.LeftShift,
                                DeleteCategory,
                                "undelete"
                            )
                        );
                    }

                    defaultHotKeyManager?.Build();
                    cycleHotKeyManager?.Build();
                    scaleHotKeyManager?.Build();
                    rotateHotKeyManager?.Build();
                    deleteHotKeyManager?.Build();
                }

                LogManager.EventTracer.Trace();
            }
            catch (System.Exception e)
            {
                LogManager.Log.NotifyBad(e);
            }
        }

        private Dictionary<string, List<CultureSettlementTemplate>> GatherTemplates()
        {
            var templates = new Dictionary<string, List<CultureSettlementTemplate>>();
            try
            {
                var loadedModules = TaleWorlds.Engine.Utilities.GetModulesNames();
                foreach (var addOnModule in GetTemplateModules())
                {
                    try
                    {
                        if (addOnModule == null || !loadedModules.Contains(addOnModule.Id))
                        {
                            // Templates available but not active for this game.
                            continue;
                        }

                        var modulePath = addOnModule?.FolderPath;
                        if (!string.IsNullOrEmpty(modulePath))
                        {
                            var subModuleFile = Path.Combine(modulePath, "SubModule.xml");
                            if (File.Exists(subModuleFile))
                            {
                                var doc = new XmlDocument();
                                doc.Load(subModuleFile);

                                var templatesDir = doc.SelectSingleNode("descendant::PlayerSettlementsTemplates")?.Attributes?["path"]?.Value;
                                var blacklistFile = doc.SelectSingleNode("descendant::PlayerSettlementsTemplatesBlacklist")?.Attributes?["path"]?.Value;
                                if (!string.IsNullOrEmpty(blacklistFile) && File.Exists(Path.Combine(modulePath, blacklistFile)))
                                {
                                    var blacklisted = File.ReadAllLines(Path.Combine(modulePath, blacklistFile)).Select(line => line?.Trim()).Where(line => !string.IsNullOrEmpty(line));
                                    _blacklistedTemplates.AddRange(blacklisted ?? new List<string>());
                                    if (addOnModule!.Id == ModuleName)
                                    {
                                        _blacklistFile = Path.Combine(modulePath, blacklistFile);
                                    }
                                }
                                if (!string.IsNullOrEmpty(templatesDir) && Directory.Exists(Path.Combine(modulePath, templatesDir)))
                                {
                                    templatesDir = Path.Combine(modulePath, templatesDir);
                                    foreach (var templateFile in Directory.GetFiles(templatesDir))
                                    {
                                        try
                                        {
                                            if (!string.IsNullOrEmpty(templateFile) && File.Exists(templateFile))
                                            {
                                                var templateDoc = new XmlDocument();
                                                try
                                                {
                                                    templateDoc.Load(templateFile);
                                                }
                                                catch (Exception e)
                                                {
                                                    LogManager.EventTracer.Trace(new List<string> { e.Message, e.StackTrace });
                                                    continue;
                                                }
                                                var cultureSettlementInfo = new CultureSettlementTemplate
                                                {
                                                    FromModule = addOnModule!.Id,
                                                    Document = templateDoc,
                                                    TemplateModifier = templateDoc.ChildNodes?[0]?.Attributes?["template_modifier"]?.Value ?? "",
                                                    CultureId = templateDoc.ChildNodes?[0]?.Attributes?["culture_template"]?.Value ?? ""

                                                };

                                                if (!templates.ContainsKey(cultureSettlementInfo.CultureId))
                                                {
                                                    templates[cultureSettlementInfo.CultureId] = new List<CultureSettlementTemplate>();
                                                }
                                                templates[cultureSettlementInfo.CultureId].Add(cultureSettlementInfo);
                                            }
                                        }
                                        catch (System.Exception e)
                                        {
                                            LogManager.Log.NotifyBad(e);
                                        }

                                    }
                                    LogManager.Log.NotifyGood($"Loaded '{addOnModule!.Name}' Templates");
                                }
                            }
                        }
                    }
                    catch (System.Exception e) { LogManager.Log.NotifyBad(e); }

                }
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }
            return templates;
        }
        public void UpdateBlacklist(params string[] newBlacklistItems)
        {
            _blacklistedTemplates.AddRange(newBlacklistItems);
            if (!string.IsNullOrEmpty(BlacklistFile))
            {
                File.AppendAllText(BlacklistFile, "\r\n");
                File.AppendAllLines(BlacklistFile, newBlacklistItems);
            }

        }

        private IEnumerable<ModuleInfo> GetTemplateModules()
        {
            var thisModule = ModuleHelper.GetModuleInfo(ModuleName);

            return (new List<ModuleInfo>() { thisModule }).Union(ModuleHelper.GetModules().Where(mi => mi != thisModule && mi.Id != thisModule.Id && mi.DependedModules.FindIndex(dp => dp.ModuleId == thisModule.Id) != -1));
        }

        protected override void OnGameStart(Game game, IGameStarter starterObject)
        {
            try
            {
                base.OnGameStart(game, starterObject);

                if (game.GameType is Campaign)
                {
                    var initializer = (CampaignGameStarter) starterObject;
                    AddBehaviors(initializer);
                }
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }
        }

        public override void RegisterSubModuleObjects(bool isSavedCampaign)
        {
            PlayerSettlementBehaviour.OldSaveLoaded = false;
            PlayerSettlementBehaviour.TriggerSaveAfterUpgrade = false;
            //return;
            if (MBObjectManager.Instance != null && isSavedCampaign)
            {
                MetaV3? metaV3 = null;
                if (PlayerSettlementBehaviour.Instance != null)
                {
                    var store = Campaign.Current.GetStore(PlayerSettlementBehaviour.Instance);

                    PlayerSettlementBehaviour.Instance.LoadEarlySync(store);

                    metaV3 = PlayerSettlementBehaviour.Instance.MetaV3;
                }

                if (metaV3 == null)
                {
                    var userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mount and Blade II Bannerlord");
                    var moduleName = Main.Name;

                    var ConfigDir = Path.Combine(userDir, "Configs", moduleName, Campaign.Current.UniqueGameId);

                    if (!Directory.Exists(ConfigDir))
                    {
                        return;
                    }


                    //MetaV3? metaV3;

                    var metaObj = MetaV1_2.ReadFile(userDir, moduleName, ref ConfigDir);
                    if (metaObj != null)
                    {
                        metaV3 = metaObj.Convert(ConfigDir);
                    }

                    if (metaV3 == null)
                    {
                        return;
                    }

                    PlayerSettlementBehaviour.TriggerSaveAfterUpgrade = true;
                }

                for (int t = 0; t < metaV3.Towns.Count; t++)
                {
                    var townMeta = metaV3.Towns[t];

                    if (townMeta.BuildTime - 5 > Campaign.CurrentTime)
                    {
                        // A player settlement has been made in a different save.
                        // This is an older save than the config is for.
                        PlayerSettlementBehaviour.OldSaveLoaded = true;
                        continue;
                    }

                    if (townMeta.settlement == null || !townMeta.settlement.IsReady)
                    {

                        var doc = townMeta.Document;

                        MBObjectManager.Instance.LoadXml(doc);

                        string townStringId;
                        if (!string.IsNullOrEmpty(townMeta.Version) && !string.IsNullOrEmpty(townMeta.StringId) && new Version(townMeta.Version).CompareTo(Feature_StringId_Version) >= 0)
                        {
                            townStringId = townMeta.StringId;
                        }
                        else
                        {
                            townStringId = $"player_settlement_town_{townMeta.Identifier}";
                        }

                        townMeta.settlement = MBObjectManager.Instance.GetObject<Settlement>(townStringId);

                        if (townMeta.settlement != null && !townMeta.settlement.IsReady)
                        {
                            MBObjectManager.Instance.UnregisterObject(townMeta.settlement);
                            MBObjectManager.Instance.LoadXml(doc);

                            townMeta.settlement = MBObjectManager.Instance.GetObject<Settlement>(townStringId);
                        }
                    }


                    if (townMeta.settlement != null)
                    {
                        if (!string.IsNullOrEmpty(townMeta.DisplayName))
                        {
                            townMeta.settlement.Party.SetCustomName(new TextObject(townMeta.DisplayName));
                        }
                    }

                    for (int i = 0; i < townMeta.Villages.Count; i++)
                    {
                        var village = townMeta.Villages[i];

                        if (village.BuildTime - 5 > Campaign.CurrentTime)
                        {
                            // A player settlement has been made in a different save.
                            // This is an older save than the config is for.
                            PlayerSettlementBehaviour.OldSaveLoaded = true;
                            continue;
                        }

                        var vDoc = village.Document;

                        string villageStringId;
                        if (!string.IsNullOrEmpty(village.Version) && !string.IsNullOrEmpty(village.StringId) && new Version(village.Version).CompareTo(Feature_StringId_Version) >= 0)
                        {
                            villageStringId = village.StringId;
                        }
                        else
                        {
                            villageStringId = $"player_settlement_town_{townMeta.Identifier}_village_{village.Identifier}";
                        }

                        village.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);

                        if (village.settlement == null || !village.settlement.IsReady)
                        {
                            MBObjectManager.Instance.LoadXml(vDoc);

                            village.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);

                            if (village.settlement != null && !village.settlement.IsReady)
                            {
                                MBObjectManager.Instance.LoadXml(vDoc);

                                village.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);
                            }
                        }


                        if (village.settlement != null)
                        {
                            if (!string.IsNullOrEmpty(village.DisplayName))
                            {
                                village.settlement.Party.SetCustomName(new TextObject(village.DisplayName));
                            }
                        }
                    }
                }

                for (int c = 0; c < metaV3.Castles.Count; c++)
                {
                    var castleId = c + 1;
                    var castleMeta = metaV3.Castles[c];

                    if (castleMeta.BuildTime - 5 > Campaign.CurrentTime)
                    {
                        // A player settlement has been made in a different save.
                        // This is an older save than the config is for.
                        PlayerSettlementBehaviour.OldSaveLoaded = true;
                        continue;
                    }

                    var doc = castleMeta.Document;

                    if (castleMeta.settlement == null || !castleMeta.settlement.IsReady)
                    {
                        string castleStringId;
                        if (!string.IsNullOrEmpty(castleMeta.Version) && !string.IsNullOrEmpty(castleMeta.StringId) && new Version(castleMeta.Version).CompareTo(Feature_StringId_Version) >= 0)
                        {
                            castleStringId = castleMeta.StringId;
                        }
                        else
                        {
                            castleStringId = $"player_settlement_castle_{castleMeta.Identifier}";
                        }

                        //var configFile = Path.Combine(ConfigDir, $"PlayerCastle_{castleId}.xml");
                        //MBObjectManager.Instance.LoadOneXmlFromFile(configFile, null, true);
                        MBObjectManager.Instance.LoadXml(doc);

                        castleMeta.settlement = MBObjectManager.Instance.GetObject<Settlement>(castleStringId);

                        if (castleMeta.settlement != null && !castleMeta.settlement.IsReady)
                        {
                            //MBObjectManager.Instance.UnregisterObject(castleMeta.settlement);
                            //MBObjectManager.Instance.LoadOneXmlFromFile(configFile, null, true);
                            MBObjectManager.Instance.LoadXml(doc);

                            castleMeta.settlement = MBObjectManager.Instance.GetObject<Settlement>(castleStringId);
                        }
                    }


                    if (castleMeta.settlement != null)
                    {
                        if (!string.IsNullOrEmpty(castleMeta.DisplayName))
                        {
                            castleMeta.settlement.Party.SetCustomName(new TextObject(castleMeta.DisplayName));
                        }
                    }

                    for (int i = 0; i < castleMeta.Villages.Count; i++)
                    {
                        var village = castleMeta.Villages[i];

                        if (village.BuildTime - 5 > Campaign.CurrentTime)
                        {
                            // A player settlement has been made in a different save.
                            // This is an older save than the config is for.
                            PlayerSettlementBehaviour.OldSaveLoaded = true;
                            continue;
                        }

                        var vDoc = village.Document;

                        string villageStringId;
                        if (!string.IsNullOrEmpty(village.Version) && !string.IsNullOrEmpty(village.StringId) && new Version(village.Version).CompareTo(Feature_StringId_Version) >= 0)
                        {
                            villageStringId = village.StringId;
                        }
                        else
                        {
                            villageStringId = $"player_settlement_castle_{castleMeta.Identifier}_village_{village.Identifier}";
                        }

                        village.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);

                        if (village.settlement == null || !village.settlement.IsReady)
                        {
                            MBObjectManager.Instance.LoadXml(vDoc);

                            village.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);

                            if (village.settlement != null && !village.settlement.IsReady)
                            {
                                MBObjectManager.Instance.LoadXml(vDoc);

                                village.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);
                            }
                        }


                        if (village.settlement != null)
                        {
                            if (!string.IsNullOrEmpty(village.DisplayName))
                            {
                                village.settlement.Party.SetCustomName(new TextObject(village.DisplayName));
                            }
                        }
                    }
                }

                if (metaV3.ExtraVillages != null)
                {
                    for (int ev = 0; ev < metaV3.ExtraVillages.Count; ev++)
                    {
                        var villageMeta = metaV3.ExtraVillages[ev];

                        if (villageMeta.BuildTime - 5 > Campaign.CurrentTime)
                        {
                            // A player settlement has been made in a different save.
                            // This is an older save than the config is for.
                            PlayerSettlementBehaviour.OldSaveLoaded = true;
                            continue;
                        }

                        string villageStringId = villageMeta.StringId;

                        MBObjectManager.Instance.LoadXml(villageMeta.Document);

                        villageMeta.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);

                        if (villageMeta.settlement != null && !villageMeta.settlement.IsReady)
                        {
                            MBObjectManager.Instance.LoadXml(villageMeta.Document);

                            villageMeta.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);
                        }


                        if (villageMeta.settlement != null)
                        {
                            if (!string.IsNullOrEmpty(villageMeta.DisplayName))
                            {
                                villageMeta.settlement.Party.SetCustomName(new TextObject(villageMeta.DisplayName));
                            }
                        }
                    }
                }

                if (metaV3.OverwriteSettlements != null)
                {
                    for (int os = 0; os < metaV3.OverwriteSettlements.Count; os++)
                    {
                        var overwriteMeta = metaV3.OverwriteSettlements[os];

                        if (overwriteMeta.BuildTime - 5 > Campaign.CurrentTime)
                        {
                            // A player settlement has been made in a different save.
                            // This is an older save than the config is for.
                            PlayerSettlementBehaviour.OldSaveLoaded = true;
                            continue;
                        }

                        string stringId = overwriteMeta.StringId;

                        MBObjectManager.Instance.LoadXml(overwriteMeta.Document);

                        overwriteMeta.settlement = MBObjectManager.Instance.GetObject<Settlement>(stringId);

                        if (overwriteMeta.settlement != null && !overwriteMeta.settlement.IsReady)
                        {
                            MBObjectManager.Instance.LoadXml(overwriteMeta.Document);

                            overwriteMeta.settlement = MBObjectManager.Instance.GetObject<Settlement>(stringId);
                        }


                        if (overwriteMeta.settlement != null)
                        {
                            if (!string.IsNullOrEmpty(overwriteMeta.DisplayName))
                            {
                                overwriteMeta.settlement.Party.SetCustomName(new TextObject(overwriteMeta.DisplayName));
                            }
                        }
                    }
                }
            }
        }

        private void AddBehaviors(CampaignGameStarter gameInitializer)
        {
            try
            {
                gameInitializer.AddBehavior(new PlayerSettlementBehaviour());

                foreach (var patch in HarmonyCompatPatches)
                {
                    patch.AddBehaviors(gameInitializer);
                }
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }
        }

        protected static void Settings_OnPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            try
            {
                if (sender is Settings settings && args.PropertyName == Settings.SaveTriggered)
                {
                    try
                    {
                        MapBarExtensionVM.Current?.OnRefresh();
                    }
                    catch (Exception) { }
                    LogManager.EventTracer.Trace();
                }
            }
            catch (System.Exception e) { LogManager.Log.NotifyBad(e); }
        }

        static IEnumerable<ICompatibilityPatch> LoadCompatPatches()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(ICompatibilityPatch).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        object? inst = null;
                        try
                        {
                            inst = type.CreateInstance();
                        }
                        catch (Exception e)
                        {
                            LogManager.Log.NotifyBad(e);
                        }

                        if (inst is ICompatibilityPatch compatibilityPatch)
                        {
                            yield return compatibilityPatch;
                        }

                    }
                }
            }
        }
    }
}