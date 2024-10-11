using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

using TaleWorlds.SaveSystem;

namespace BannerlordPlayerSettlement.Saves
{
    [JsonObject(MemberSerialization.OptIn)]
    public class MetaV3
    {
        [JsonProperty]
        [SaveableProperty(301)]
        public string SavedModuleVersion { get; set; } = Main.Version;

        [JsonProperty]
        [SaveableProperty(302)]
        public List<SettlementMetaV3> Towns { get; set; } = new();

        [JsonProperty]
        [SaveableProperty(303)]
        public List<SettlementMetaV3> Castles { get; set; } = new();

        [JsonProperty]
        [SaveableProperty(304)]
        public List<SettlementMetaV3> ExtraVillages { get; set; } = new();

        public static MetaV3? Create(PlayerSettlementInfo playerSettlementInfo)
        {
            if (playerSettlementInfo?.Towns == null)
            {
                return null;
            }

            var metaV3 = new MetaV3
            {
                SavedModuleVersion = Main.Version,
                Castles = new(),
                Towns = new(),
                ExtraVillages = new()
            };

            foreach (var town in playerSettlementInfo.Towns)
            {
                metaV3.Towns.Add(new SettlementMetaV3
                {
                    XML = town.ItemXML,
                    BuildTime = town.BuiltAt,
                    DisplayName = town.SettlementName,
                    Identifier = town.Identifier,
                    settlement = town.Settlement,
                    StringId = town.StringId,
                    Version = town.Version,
                    Villages = town.Villages.Select(v => new SettlementMetaV3
                    {
                        XML = v.ItemXML,
                        BuildTime = v.BuiltAt,
                        DisplayName = v.SettlementName,
                        Identifier = v.Identifier,
                        settlement = v.Settlement,
                        StringId = v.StringId,
                        Version = v.Version,
                        Villages = new()
                    }).ToList()
                });
            }
            foreach (var castle in playerSettlementInfo.Castles)
            {
                metaV3.Castles.Add(new SettlementMetaV3
                {
                    XML = castle.ItemXML,
                    BuildTime = castle.BuiltAt,
                    DisplayName = castle.SettlementName,
                    Identifier = castle.Identifier,
                    settlement = castle.Settlement,
                    StringId = castle.StringId,
                    Version = castle.Version,
                    Villages = castle.Villages.Select(v => new SettlementMetaV3
                    {
                        XML = v.ItemXML,
                        BuildTime = v.BuiltAt,
                        DisplayName = v.SettlementName,
                        Identifier = v.Identifier,
                        settlement = v.Settlement,
                        StringId = v.StringId,
                        Version = v.Version,
                        Villages = new()
                    }).ToList()
                });
            }

            if (playerSettlementInfo.PlayerVillages != null)
            {
                foreach (var v in playerSettlementInfo.PlayerVillages)
                {
                    metaV3.ExtraVillages.Add(new SettlementMetaV3
                    {
                        XML = v.ItemXML,
                        BuildTime = v.BuiltAt,
                        DisplayName = v.SettlementName,
                        Identifier = v.Identifier,
                        settlement = v.Settlement,
                        StringId = v.StringId,
                        Version = v.Version,
                        Villages = new(),
                    });
                }
            }

            return metaV3;
        }
    }
}
