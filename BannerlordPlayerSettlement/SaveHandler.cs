using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
        private static SaveHandler _instance = new SaveHandler();
        public static SaveHandler Instance => _instance;


        static FieldInfo ActiveSaveSlotNameField = AccessTools.Field(typeof(MBSaveLoad), "ActiveSaveSlotName");
        static MethodInfo GetNextAvailableSaveNameMethod = AccessTools.Method(typeof(MBSaveLoad), "GetNextAvailableSaveName");

        public static void SaveLoad(Action<string>? afterSave = null)
        {
            Instance.SaveAndLoad(afterSave);
        }
        public static void SaveOnly(bool overwrite = true)
        {
            Instance.Save(overwrite);
        }

        public void SaveAndLoad(Action<string>? afterSave = null)
        {
            CampaignEvents.OnSaveOverEvent.AddNonSerializedListener(Instance, new Action<bool, string>((b, s) => Instance.ApplyInternal(b, s, afterSave)));

            string saveName = (string) ActiveSaveSlotNameField.GetValue(null);
            if (saveName == null)
            {
                saveName = (string) GetNextAvailableSaveNameMethod.Invoke(null, new object[] { });
                ActiveSaveSlotNameField.SetValue(null, saveName);
            }
            Campaign.Current.SaveHandler.SaveAs(saveName + new TextObject("{=player_settlement_n_02} (auto)").ToString());

            // Save over current as previous saves will be corrupt!
            //Campaign.Current.SaveHandler.SaveAs(saveName);
        }

        public void Save(bool overwrite = true)
        {
            string saveName = (string) ActiveSaveSlotNameField.GetValue(null);
            if (saveName == null)
            {
                saveName = (string) GetNextAvailableSaveNameMethod.Invoke(null, new object[] { });
                ActiveSaveSlotNameField.SetValue(null, saveName);
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


        private void ApplyInternal(bool isSaveSuccessful, string newSaveGameName, Action<string>? afterSave = null)
        {
            CampaignEvents.OnSaveOverEvent.ClearListeners(this);

            if (!isSaveSuccessful)
            {
                return;
            }

            if (afterSave != null)
            {
                afterSave.Invoke(newSaveGameName);
            }

            SaveGameFileInfo saveFileWithName = MBSaveLoad.GetSaveFileWithName(newSaveGameName);
            if (saveFileWithName != null && !saveFileWithName.IsCorrupted)
            {
                SandBoxSaveHelper.TryLoadSave(saveFileWithName, new Action<LoadResult>(this.StartGame), null);
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
