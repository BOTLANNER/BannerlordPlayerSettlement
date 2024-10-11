using System;
using System.Collections;
using System.Collections.Generic;

using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Extensions
{
    public static class CampaignExtensions
    {
        public static CampaignBehaviorManager? GetCampaignBehaviorManager(this Campaign campaign)
        {
            if (campaign == null)
            {
                return null;
            }

            return AccessTools.Field(typeof(Campaign), "_campaignBehaviorManager").GetValue(campaign) as CampaignBehaviorManager;
        }

        public static object? GetCampaignBehaviorDataStore(this Campaign campaign)
        {
            return campaign.GetCampaignBehaviorManager()?.GetCampaignBehaviorDataStore();
        }

        public static object? GetCampaignBehaviorDataStore(this CampaignBehaviorManager manager)
        {
            if (manager == null)
            {
                return null;
            }

            return AccessTools.Field(typeof(CampaignBehaviorManager), "_campaignBehaviorDataStore").GetValue(manager);
        }


        public static IDataStore? GetStore(this Campaign campaign, CampaignBehaviorBase campaignBehavior)
        {
            return campaign.GetCampaignBehaviorManager()?.GetStore(campaignBehavior);
        }

        public static IDataStore? GetStore(this CampaignBehaviorManager manager, CampaignBehaviorBase campaignBehavior)
        {
            try
            {
                var _campaignBehaviorDataStore = manager.GetCampaignBehaviorDataStore();
                if (_campaignBehaviorDataStore == null)
                {
                    return null;
                }

                var _behaviorDict = AccessTools.Field(_campaignBehaviorDataStore.GetType(), "_behaviorDict").GetValue(_campaignBehaviorDataStore) as IDictionary;

                if (_behaviorDict == null)
                {
                    return null;
                }

                IDataStore? behaviorSaveDatum;
                string stringId = campaignBehavior.StringId;
                if (_behaviorDict.Contains(stringId))
                {
                    behaviorSaveDatum = _behaviorDict[stringId] as IDataStore;
                    return behaviorSaveDatum;
                }

                //if (_behaviorDict.TryGetValue(stringId, out behaviorSaveDatum))
                //{
                //    return behaviorSaveDatum;
                //}
                List<KeyValuePair<string, IDataStore>> list = new List<KeyValuePair<string, IDataStore>>();
                foreach (System.Collections.DictionaryEntry item in _behaviorDict)
                {
                    list.Add(new KeyValuePair<string, IDataStore>(item.Key as string, item.Value as IDataStore));
                }
                string name = campaignBehavior.GetType().Name;
                foreach (KeyValuePair<string, IDataStore> keyValuePair in list)
                {
                    if (!keyValuePair.Key.Contains(name))
                    {
                        continue;
                    }
                    _behaviorDict.Remove(keyValuePair.Key);
                    _behaviorDict.Add(stringId, keyValuePair.Value);

                    behaviorSaveDatum = keyValuePair.Value;
                    return behaviorSaveDatum;
                }
            }
            catch (Exception e)
            {
                TaleWorlds.Library.Debug.PrintError(e.Message, e.StackTrace);
                Debug.WriteDebugLineOnScreen(e.ToString());
                Debug.SetCrashReportCustomString(e.Message);
                Debug.SetCrashReportCustomStack(e.StackTrace);
            }

            return null;
        }
    }
}
