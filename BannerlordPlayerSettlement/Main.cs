using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

using Bannerlord.UIExtenderEx;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Descriptors;
using BannerlordPlayerSettlement.Extensions;
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

namespace BannerlordPlayerSettlement
{
    public class Main : MBSubModuleBase
    {
        public static readonly string Version = $"{typeof(Main).Assembly.GetName().Version}";

        public static readonly string Name = typeof(Main).Namespace;
        public static readonly string DisplayName = "Player Settlement"; // to be shown to humans in-game
        public static readonly string HarmonyDomain = "com.b0tlanner.bannerlord." + Name.ToLower();
        public static readonly string ModuleName = "PlayerSettlement";

        public static Settings? Settings;

        private bool _loaded;
        public static Harmony? Harmony;
        private UIExtender? _extender;

        public static Main? Submodule = null;

        private static List<ICompatibilityPatch> HarmonyCompatPatches = LoadCompatPatches().ToList();

        public Dictionary<string, List<CultureSettlementTemplate>> CultureTemplates;

        private string? _blacklistFile;
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
                                                    CultureId = templateDoc.ChildNodes?[0]?.Attributes?["culture_template"]?.Value ?? "",
                                                    Castles = int.Parse(templateDoc.ChildNodes?[0]?.Attributes?["castles"]?.Value),
                                                    Towns = int.Parse(templateDoc.ChildNodes?[0]?.Attributes?["towns"]?.Value),
                                                    Villages = int.Parse(templateDoc.ChildNodes?[0]?.Attributes?["villages"]?.Value),

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
                        MBObjectManager.Instance.LoadXml(townMeta.Document);

                        string townStringId;
                        if (!string.IsNullOrEmpty(townMeta.Version) && !string.IsNullOrEmpty(townMeta.StringId) && new Version(townMeta.Version).CompareTo(new Version(Main.Version)) >= 0)
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
                            MBObjectManager.Instance.LoadXml(townMeta.Document);

                            townMeta.settlement = MBObjectManager.Instance.GetObject<Settlement>(townStringId);
                        }
                    }


                    if (townMeta.settlement != null)
                    {
                        if (!string.IsNullOrEmpty(townMeta.DisplayName))
                        {
                            townMeta.settlement.Name = new TextObject(townMeta.DisplayName);
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

                        string villageStringId;
                        if (!string.IsNullOrEmpty(village.Version) && !string.IsNullOrEmpty(village.StringId) && new Version(village.Version).CompareTo(new Version(Main.Version)) >= 0)
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
                            MBObjectManager.Instance.LoadXml(village.Document);

                            village.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);

                            if (village.settlement != null && !village.settlement.IsReady)
                            {
                                MBObjectManager.Instance.LoadXml(village.Document);

                                village.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);
                            }
                        }


                        if (village.settlement != null)
                        {
                            if (!string.IsNullOrEmpty(village.DisplayName))
                            {
                                village.settlement.Name = new TextObject(village.DisplayName);
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

                    if (castleMeta.settlement == null || !castleMeta.settlement.IsReady)
                    {
                        string castleStringId;
                        if (!string.IsNullOrEmpty(castleMeta.Version) && !string.IsNullOrEmpty(castleMeta.StringId) && new Version(castleMeta.Version).CompareTo(new Version(Main.Version)) >= 0)
                        {
                            castleStringId = castleMeta.StringId;
                        }
                        else
                        {
                            castleStringId = $"player_settlement_castle_{castleMeta.Identifier}";
                        }

                        //var configFile = Path.Combine(ConfigDir, $"PlayerCastle_{castleId}.xml");
                        //MBObjectManager.Instance.LoadOneXmlFromFile(configFile, null, true);
                        MBObjectManager.Instance.LoadXml(castleMeta.Document);

                        castleMeta.settlement = MBObjectManager.Instance.GetObject<Settlement>(castleStringId);

                        if (castleMeta.settlement != null && !castleMeta.settlement.IsReady)
                        {
                            //MBObjectManager.Instance.UnregisterObject(castleMeta.settlement);
                            //MBObjectManager.Instance.LoadOneXmlFromFile(configFile, null, true);
                            MBObjectManager.Instance.LoadXml(castleMeta.Document);

                            castleMeta.settlement = MBObjectManager.Instance.GetObject<Settlement>(castleStringId);
                        }
                    }


                    if (castleMeta.settlement != null)
                    {
                        if (!string.IsNullOrEmpty(castleMeta.DisplayName))
                        {
                            castleMeta.settlement.Name = new TextObject(castleMeta.DisplayName);
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

                        string villageStringId;
                        if (!string.IsNullOrEmpty(village.Version) && !string.IsNullOrEmpty(village.StringId) && new Version(village.Version).CompareTo(new Version(Main.Version)) >= 0)
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
                            MBObjectManager.Instance.LoadXml(village.Document);

                            village.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);

                            if (village.settlement != null && !village.settlement.IsReady)
                            {
                                MBObjectManager.Instance.LoadXml(village.Document);

                                village.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);
                            }
                        }


                        if (village.settlement != null)
                        {
                            if (!string.IsNullOrEmpty(village.DisplayName))
                            {
                                village.settlement.Name = new TextObject(village.DisplayName);
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
                                villageMeta.settlement.Name = new TextObject(villageMeta.DisplayName);
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