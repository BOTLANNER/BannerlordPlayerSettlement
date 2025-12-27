using System;
using System.Reflection;

using HarmonyLib;

using SandBox;

using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.ScreenSystem;

namespace BannerlordPlayerSettlement
{
    public class SaveHandler
    {
        public enum SaveMechanism
        {
            Overwrite = 0,
            Auto = 1,
            Temporary = 2
        }

        private static SaveHandler _instance = new SaveHandler();
        public static SaveHandler Instance => _instance;


        static PropertyInfo ActiveSaveSlotNameProp = AccessTools.Property(typeof(MBSaveLoad), "ActiveSaveSlotName");
        static MethodInfo GetNextAvailableSaveNameMethod = AccessTools.Method(typeof(MBSaveLoad), "GetNextAvailableSaveName");

        public static void SaveLoad(SaveMechanism saveMechanism = SaveMechanism.Overwrite, Action<SaveMechanism, string>? afterSave = null)
        {
            Instance.SaveAndLoad(saveMechanism, afterSave);
        }

        public static void SaveOnly(bool overwrite = true)
        {
            Instance.Save(overwrite);
        }

        public void SaveAndLoad(SaveMechanism saveMechanism = SaveMechanism.Overwrite, Action<SaveMechanism, string>? afterSave = null)
        {
            string saveName = (string) ActiveSaveSlotNameProp.GetValue(null);
            if (saveName == null)
            {
                saveName = (string) GetNextAvailableSaveNameMethod.Invoke(null, new object[] { });
                ActiveSaveSlotNameProp.SetValue(null, saveName);
            }

            CampaignEvents.OnSaveOverEvent.AddNonSerializedListener(Instance, new Action<bool, string>((b, s) => Instance.ApplyInternal(saveMechanism, saveName, b, s, afterSave)));


            if (saveMechanism == SaveMechanism.Overwrite)
            {
                Campaign.Current.SaveHandler.SaveAs(saveName);
            }
            else
            {
                Campaign.Current.SaveHandler.SaveAs(saveName + new TextObject("{=player_settlement_n_02} (auto)").ToString());
            }
        }

        public void Save(bool overwrite = true)
        {
            string saveName = (string) ActiveSaveSlotNameProp.GetValue(null);
            if (saveName == null)
            {
                saveName = (string) GetNextAvailableSaveNameMethod.Invoke(null, new object[] { });
                ActiveSaveSlotNameProp.SetValue(null, saveName);
            }

            if (overwrite)
            {
                Campaign.Current.SaveHandler.SaveAs(saveName);
            }
            else
            {
                Campaign.Current.SaveHandler.SaveAs(saveName + new TextObject("{=player_settlement_n_02} (auto)").ToString());
            }
        }


        private void ApplyInternal(SaveMechanism saveMechanism, string originalSaveName, bool isSaveSuccessful, string newSaveGameName, Action<SaveMechanism, string>? afterSave = null)
        {
            CampaignEvents.OnSaveOverEvent.ClearListeners(this);

            if (!isSaveSuccessful)
            {
                return;
            }

            if (afterSave != null)
            {
                afterSave.Invoke(saveMechanism, newSaveGameName);
            }

            SaveGameFileInfo saveFileWithName = MBSaveLoad.GetSaveFileWithName(newSaveGameName);
            if (saveFileWithName != null && !saveFileWithName.IsCorrupted)
            {
                SandBoxSaveHelper.TryLoadSave(saveFileWithName, new Action<LoadResult>((loadResult) =>
                {
                    if (saveMechanism == SaveMechanism.Temporary)
                    {
                        MBSaveLoad.DeleteSaveGame(newSaveGameName);
                        SaveGameFileInfo saveFileWithName = MBSaveLoad.GetSaveFileWithName(originalSaveName);
                        if (saveFileWithName != null && !saveFileWithName.IsCorrupted)
                        {
                            ActiveSaveSlotNameProp.SetValue(null, originalSaveName);
                        }
                    }
                    this.StartGame(loadResult);
                }), null);
                return;
            }
            InformationManager.ShowInquiry(new InquiryData((new TextObject("{=oZrVNUOk}Error", null)).ToString(), (new TextObject("{=t6W3UjG0}Save game file appear to be corrupted. Try starting a new campaign or load another one from Saved Games menu.", null)).ToString(), true, false, (new TextObject("{=yS7PvrTD}OK", null)).ToString(), null, null, null, "", 0f, null, null, null), false, false);



        }

        public void StartGame(LoadResult loadResult)
        {
            if (Game.Current != null)
            {
                ScreenManager.PopScreen();
                GameStateManager.Current.CleanStates(0);
                GameStateManager.Current = TaleWorlds.MountAndBlade.Module.CurrentModule.GlobalGameStateManager;
            }
            MBSaveLoad.OnStartGame(loadResult);
            MBGameManager.StartNewGame(new SandBoxGameManager(loadResult));
        }
    }
}
