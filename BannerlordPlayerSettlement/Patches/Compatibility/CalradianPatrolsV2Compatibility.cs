using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Patches.Compatibility.Interfaces;
using BannerlordPlayerSettlement.Saves;

using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Patches.Compatibility
{
    // CalradianPatrolsV2
    public class CalradianPatrolsV2Compatibility : ICompatibilityPatch
    {
        public bool IsEnabled => assembly != null && behaviorType != null;

        private Assembly? assembly;
        private Type? behaviorType;

        //  private Dictionary<Settlement, bool> _autoRecruits = new Dictionary<Settlement, bool>();
        private FieldInfo? _autoRecruitsField;

        public void AddBehaviors(CampaignGameStarter gameInitializer)
        {
            if (IsEnabled)
            {
                Behaviour campaignBehavior = new Behaviour
                {
                    Owner = this
                };
                gameInitializer.AddBehavior(campaignBehavior);
            }
        }

        public void PatchAfterMenus(Harmony harmony)
        {
        }

        public void PatchSubmoduleLoad(Harmony harmony)
        {
            assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWith("CalradianPatrolsV2, ", StringComparison.InvariantCultureIgnoreCase));

            if (assembly != null)
            {
                behaviorType = assembly.GetType("CalradianPatrols.Behaviors.PatrolsCampaignBehavior", false, true);
                if (behaviorType != null)
                {
                    _autoRecruitsField = AccessTools.Field(behaviorType, "_autoRecruits");

                    harmony.Patch(AccessTools.Method(behaviorType, "OnSettlementEntered"), prefix: new HarmonyMethod(typeof(CalradianPatrolsV2Compatibility), nameof(OnSettlementEntered)));
                    harmony.Patch(AccessTools.Method(behaviorType, "HourlyTickSettlement"), prefix: new HarmonyMethod(typeof(CalradianPatrolsV2Compatibility), nameof(HourlyTickSettlement)));

                }
            }
        }

        private static void HourlyTickSettlement(ref Dictionary<Settlement, bool> ____autoRecruits, object __instance, Settlement settlement)
        {
            var _autoRecruits = ____autoRecruits;
            if (settlement != null && _autoRecruits != null && settlement.IsTown && !_autoRecruits.ContainsKey(settlement) && settlement.IsPlayerBuilt())
            {
                _autoRecruits[settlement] = false;
            }
        }

        private static void OnSettlementEntered(ref Dictionary<Settlement, bool> ____autoRecruits, object __instance, MobileParty patrolParty, Settlement settlement, Hero hero)
        {
            var _autoRecruits = ____autoRecruits;

            UpdateKnownTowns(_autoRecruits);
        }

        private static void UpdateKnownTowns(Dictionary<Settlement, bool> _autoRecruits)
        {
            if (_autoRecruits != null && PlayerSettlementInfo.Instance != null)
            {
                var towns = CollectPlayerBuiltTowns();
                foreach (var town in towns)
                {
                    if (town != null && town.IsTown && !_autoRecruits.ContainsKey(town))
                    {
                        _autoRecruits[town] = false;
                    }
                }
            }
        }

        private static List<Settlement> CollectPlayerBuiltTowns()
        {
            var towns = new List<Settlement>();
            if (PlayerSettlementInfo.Instance == null)
            {
                return towns;
            }
            towns.AddRange(PlayerSettlementInfo.Instance.Towns?.Select(t => t.Settlement!) ?? new List<Settlement>());
            return towns;
        }

        public class Behaviour : CampaignBehaviorBase
        {
            public CalradianPatrolsV2Compatibility? Owner = null;

            public override void RegisterEvents()
            {
                PlayerSettlementBehaviour.SettlementCreatedEvent?.AddNonSerializedListener(this, (settlement) =>
                {
                    try
                    {
                        if (!settlement.IsTown)
                        {
                            return;
                        }

                        var campaignGameStarter = SandBoxManager.Instance.GameStarter;
                        var PatrolsCampaignBehavior = campaignGameStarter.CampaignBehaviors.FirstOrDefault(b => b.GetType() == Owner?.behaviorType);
                        if (PatrolsCampaignBehavior is CampaignBehaviorBase behaviour && Owner?._autoRecruitsField != null)
                        {
                            var obj = Owner._autoRecruitsField.GetValue(behaviour);
                            if (obj is Dictionary<Settlement, bool> _autoRecruits)
                            {
                                _autoRecruits[settlement] = false;
                                UpdateKnownTowns(_autoRecruits);
                            }

                        }
                    }
                    catch (Exception e)
                    {
                        Debug.PrintError(e.ToString(), e.StackTrace, 281474976710656L);
                    }

                });
            }

            public override void SyncData(IDataStore dataStore)
            {
            }
        }
    }
}
