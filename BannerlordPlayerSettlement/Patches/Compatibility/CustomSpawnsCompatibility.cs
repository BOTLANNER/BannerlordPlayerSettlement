using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Patches.Compatibility.Interfaces;
using BannerlordPlayerSettlement.Saves;

using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Patches.Compatibility
{

    // CalradiaAtWar / CustomSpawns
    public class CustomSpawnsCompatibility : ICompatibilityPatch
    {
        public bool IsEnabled => customSpawnsAssembly != null && DevestationMetricDataBehaviorType != null;

        private Assembly? customSpawnsAssembly;
        private Type? DevestationMetricDataBehaviorType;
        private FieldInfo? _settlementToDevestationField;

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
            customSpawnsAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWith("CustomSpawns, "));

            if (customSpawnsAssembly != null)
            {
                DevestationMetricDataBehaviorType = customSpawnsAssembly.GetType("CustomSpawns.CampaignData.Implementations.DevestationMetricData", false, true);
                if (DevestationMetricDataBehaviorType != null)
                {
                    _settlementToDevestationField = AccessTools.Field(DevestationMetricDataBehaviorType, "_settlementToDevestation");

                    harmony.Patch(AccessTools.Method(DevestationMetricDataBehaviorType, "OnMapEventEnded"), prefix: new HarmonyMethod(typeof(CustomSpawnsCompatibility), nameof(OnMapEventEnded)));
                    harmony.Patch(AccessTools.Method(DevestationMetricDataBehaviorType, "GetDevestation"), prefix: new HarmonyMethod(typeof(CustomSpawnsCompatibility), nameof(GetDevestation)));
                    harmony.Patch(AccessTools.Method(DevestationMetricDataBehaviorType, "OnSettlementDaily"), prefix: new HarmonyMethod(typeof(CustomSpawnsCompatibility), nameof(OnSettlementDaily)));

                }
            }
        }

        private static void OnSettlementDaily(ref Dictionary<Settlement, float> ____settlementToDevestation, object __instance, Settlement s)
        {
            var _settlementToDevestation = ____settlementToDevestation;
            if (s != null && _settlementToDevestation != null && s.IsVillage && !_settlementToDevestation.ContainsKey(s) && s.IsPlayerBuilt())
            {
                _settlementToDevestation[s] = 0f;
            }
        }

        private static void GetDevestation(ref float __result, ref Dictionary<Settlement, float> ____settlementToDevestation, object __instance, Settlement s)
        {
            var _settlementToDevestation = ____settlementToDevestation;
            if (s != null && _settlementToDevestation != null && s.IsVillage && !_settlementToDevestation.ContainsKey(s) && s.IsPlayerBuilt())
            {
                _settlementToDevestation[s] = 0f;
            }
        }

        private static void OnMapEventEnded(ref Dictionary<Settlement, float> ____settlementToDevestation, object __instance, MapEvent e)
        {
            var _settlementToDevestation = ____settlementToDevestation;

            UpdateKnownVillages(_settlementToDevestation);
        }

        private static void UpdateKnownVillages(Dictionary<Settlement, float> _settlementToDevestation)
        {
            if (_settlementToDevestation != null && PlayerSettlementInfo.Instance != null)
            {
                var villages = CollectPlayerBuiltVillages();
                foreach (var village in villages)
                {
                    if (village != null && village.IsVillage && !_settlementToDevestation.ContainsKey(village))
                    {
                        _settlementToDevestation[village] = 0f;
                    }
                }
            }
        }

        private static List<Settlement> CollectPlayerBuiltVillages()
        {
            var villages = new List<Settlement>();
            if (PlayerSettlementInfo.Instance == null)
            {
                return villages;
            }
            villages.AddRange(PlayerSettlementInfo.Instance.Towns?.SelectMany(t => t.Villages?.Select(v => v.Settlement!)) ?? new List<Settlement>());
            villages.AddRange(PlayerSettlementInfo.Instance.Castles?.SelectMany(c => c.Villages?.Select(v => v.Settlement!)) ?? new List<Settlement>());
            return villages;
        }

        public class Behaviour : CampaignBehaviorBase
        {
            public CustomSpawnsCompatibility? Owner = null;

            public override void RegisterEvents()
            {
                PlayerSettlementBehaviour.SettlementCreatedEvent?.AddNonSerializedListener(this, (settlement) =>
                {
                    try
                    {
                        if (!settlement.IsVillage)
                        {
                            return;
                        }

                        var campaignGameStarter = SandBoxManager.Instance.GameStarter;
                        var devestationMetricDataBehavior = campaignGameStarter.CampaignBehaviors.FirstOrDefault(b => b.GetType() == Owner?.DevestationMetricDataBehaviorType);
                        if (devestationMetricDataBehavior is CampaignBehaviorBase behaviour && Owner?._settlementToDevestationField != null)
                        {
                            //_settlementToDevestation = new Dictionary<Settlement, float>();
                            var _settlementToDevestationObj = Owner._settlementToDevestationField.GetValue(behaviour);
                            if (_settlementToDevestationObj is Dictionary<Settlement, float> _settlementToDevestation)
                            {
                                _settlementToDevestation[settlement] = 0f;
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
