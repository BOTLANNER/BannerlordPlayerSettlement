using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

using Bannerlord.UIExtenderEx;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Saves;
using BannerlordPlayerSettlement.UI;
using BannerlordPlayerSettlement.Utils;

using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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
                Harmony = new Harmony(HarmonyDomain);
                Harmony.PatchAll();

                _extender = UIExtender.Create(ModuleName); //HarmonyDomain);
                _extender.Register(typeof(Main).Assembly);
                _extender.Enable();
            }
            catch (System.Exception e)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace);
                Debug.WriteDebugLineOnScreen(e.ToString());
                Debug.SetCrashReportCustomString(e.Message);
                Debug.SetCrashReportCustomStack(e.StackTrace);
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
                }

                if (!_loaded)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"Loaded {DisplayName}", Colours.ImportantTextColor));
                    _loaded = true;
                }
            }
            catch (System.Exception e)
            {
                TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace);
                Debug.WriteDebugLineOnScreen(e.ToString());
                Debug.SetCrashReportCustomString(e.Message);
                Debug.SetCrashReportCustomStack(e.StackTrace);
            }
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
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
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
                    var townId = t + 1;
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
                        //var configFile = Path.Combine(ConfigDir, $"PlayerTown_{townId}.xml");
                        //MBObjectManager.Instance.LoadOneXmlFromFile(configFile, null, true);
                        MBObjectManager.Instance.LoadXml(townMeta.Document);

                        string townStringId = $"player_settlement_town_{townMeta.Identifier}";

                        townMeta.settlement = MBObjectManager.Instance.GetObject<Settlement>(townStringId);

                        if (townMeta.settlement != null && !townMeta.settlement.IsReady)
                        {
                            MBObjectManager.Instance.UnregisterObject(townMeta.settlement);
                            //MBObjectManager.Instance.LoadOneXmlFromFile(configFile, null, true);
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

                        var villageNumber = i + 1;

                        string villageStringId = $"player_settlement_town_{townMeta.Identifier}_village_{village.Identifier}";

                        village.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);

                        if (village.settlement == null || !village.settlement.IsReady)
                        {
                            //var configFile = Path.Combine(ConfigDir, $"PlayerTown_{townId}_Village_{villageNumber}.xml");
                            //MBObjectManager.Instance.LoadOneXmlFromFile(configFile, null, true);
                            MBObjectManager.Instance.LoadXml(village.Document);

                            village.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);

                            if (village.settlement != null && !village.settlement.IsReady)
                            {
                                //configFile = Path.Combine(ConfigDir, $"PlayerTown_{townMeta.Identifier}_Village_{villageNumber}.xml");
                                //MBObjectManager.Instance.LoadOneXmlFromFile(configFile, null, true);
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
                        var castleStringId = $"player_settlement_castle_{castleMeta.Identifier}";

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

                        var villageNumber = i + 1;

                        string villageStringId = $"player_settlement_castle_{castleMeta.Identifier}_village_{village.Identifier}";

                        village.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);

                        if (village.settlement == null || !village.settlement.IsReady)
                        {
                            //var configFile = Path.Combine(ConfigDir, $"PlayerCastle_{castleId}_Village_{villageNumber}.xml");
                            //MBObjectManager.Instance.LoadOneXmlFromFile(configFile, null, true);
                            MBObjectManager.Instance.LoadXml(village.Document);

                            village.settlement = MBObjectManager.Instance.GetObject<Settlement>(villageStringId);

                            if (village.settlement != null && !village.settlement.IsReady)
                            {
                                //MBObjectManager.Instance.UnregisterObject(village.settlement);
                                //MBObjectManager.Instance.LoadOneXmlFromFile(configFile, null, true);
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
            }
        }

        private void AddBehaviors(CampaignGameStarter gameInitializer)
        {
            try
            {
                gameInitializer.AddBehavior(new PlayerSettlementBehaviour());
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
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
                }
            }
            catch (System.Exception e) { TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace); Debug.WriteDebugLineOnScreen(e.ToString()); Debug.SetCrashReportCustomString(e.Message); Debug.SetCrashReportCustomStack(e.StackTrace); }
        }
    }
}
