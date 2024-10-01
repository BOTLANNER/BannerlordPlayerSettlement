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

        internal static readonly Color ImportantTextColor = Color.FromUint(0x00F16D26); // orange

        internal static Settings? Settings;

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
                    InformationManager.DisplayMessage(new InformationMessage($"Loaded {DisplayName}", ImportantTextColor));
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
            //return;
            if (MBObjectManager.Instance != null && isSavedCampaign)
            {
                Settlement? playerSettlement;

                var userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mount and Blade II Bannerlord");
                var moduleName = Main.Name;

                var ConfigDir = Path.Combine(userDir, "Configs", moduleName, Campaign.Current.UniqueGameId);

                if (!Directory.Exists(ConfigDir))
                {
                    return;
                }

                string displayName;
                string identifier;
                float buildTime;

                string savedModuleVersion;

                var metaFile = Path.Combine(ConfigDir, $"meta.bin");
                if (File.Exists(metaFile))
                {
                    string metaText = File.ReadAllText(metaFile);
                    var parts = metaText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    identifier = parts[0].Base64Decode();
                    displayName = parts[1].Base64Decode();
                    if (!float.TryParse(parts[2].Base64Decode(), out buildTime) && !float.TryParse(parts[2], out buildTime))
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"Unable to read save data!", ImportantTextColor));
                        PlayerSettlementBehaviour.OldSaveLoaded = true;
                        return;
                    }
                    savedModuleVersion = parts.Length > 3 ? parts[3].Base64Decode() : "0.0.0";

                    if (savedModuleVersion != Version)
                    {
                        // TODO: Any version specific updates here

                        // Save with latest version to indicate compatibility
                        metaText = "";
                        metaText += identifier.Base64Encode();
                        metaText += "\r\n";
                        metaText += displayName.Base64Encode();
                        metaText += "\r\n";
                        metaText += buildTime.ToString().Base64Encode();
                        metaText += "\r\n";
                        metaText += Main.Version.Base64Encode();
                        File.WriteAllText(metaFile, metaText);

                        InformationManager.DisplayMessage(new InformationMessage($"Updated {DisplayName} to {Version}", ImportantTextColor));
                    }


                    playerSettlement = MBObjectManager.Instance.GetObject<Settlement>(identifier);
                }
                else
                {
                    // No player settlement has been made
                    return;
                }

                if (buildTime - 5 > Campaign.CurrentTime)
                {
                    // A player settlement has been made in a different save.
                    // This is an older save than the config is for.
                    PlayerSettlementBehaviour.OldSaveLoaded = true;
                    return;
                }

                if (playerSettlement == null || !playerSettlement.IsReady)
                {
                    var configFile = Path.Combine(ConfigDir, $"PlayerSettlement.xml");
                    MBObjectManager.Instance.LoadOneXmlFromFile(configFile, null, true);

                    playerSettlement = MBObjectManager.Instance.GetObject<Settlement>(identifier);

                    if (playerSettlement != null && !playerSettlement.IsReady)
                    {
                        MBObjectManager.Instance.UnregisterObject(playerSettlement);
                        MBObjectManager.Instance.LoadOneXmlFromFile(configFile, null, true);

                        playerSettlement = MBObjectManager.Instance.GetObject<Settlement>(identifier);
                    }
                }


                if (playerSettlement != null)
                {
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        playerSettlement.Name = new TextObject(displayName);
                    }
                    if (PlayerSettlementInfo.Instance != null)
                    {
                        PlayerSettlementInfo.Instance.PlayerSettlement = playerSettlement;
                        PlayerSettlementInfo.Instance.PlayerSettlementIdentifier = identifier;
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
