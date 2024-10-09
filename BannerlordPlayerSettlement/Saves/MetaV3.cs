using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json;

using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
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

        //internal bool Save()
        //{
        //    try
        //    {
        //        var metaText = JsonConvert.SerializeObject(this);

        //        var userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mount and Blade II Bannerlord");
        //        var moduleName = Main.Name;

        //        var ConfigDir = Path.Combine(userDir, "Configs", moduleName, Campaign.Current.UniqueGameId);

        //        if (!Directory.Exists(ConfigDir))
        //        {
        //            Directory.CreateDirectory(ConfigDir);
        //        }

        //        var metaFile = Path.Combine(ConfigDir, $"metaV3.json");
        //        File.WriteAllText(metaFile, metaText);

        //        return true;
        //    }
        //    catch (Exception e)
        //    {
        //        Debug.PrintError(e.Message, e.StackTrace);
        //        Debug.WriteDebugLineOnScreen(e.ToString());
        //        Debug.SetCrashReportCustomString(e.Message);
        //        Debug.SetCrashReportCustomStack(e.StackTrace);
        //        return false;
        //    }
        //}

        //internal static MetaV3? Load()
        //{

        //    var userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mount and Blade II Bannerlord");
        //    var moduleName = Main.Name;

        //    var ConfigDir = Path.Combine(userDir, "Configs", moduleName, Campaign.Current.UniqueGameId);

        //    if (!Directory.Exists(ConfigDir))
        //    {
        //        Directory.CreateDirectory(ConfigDir);
        //    }
        //    var metaFile = Path.Combine(ConfigDir, $"metaV3.json");

        //    if (!File.Exists(metaFile))
        //    {
        //        return null;
        //    }

        //    var metaText = File.ReadAllText(metaFile);

        //    var metaObj = JsonConvert.DeserializeObject<MetaV3>(metaText);

        //    return metaObj;
        //}

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
                Towns = new()
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

            return metaV3;
        }
    }
}
