using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Extensions;
using BannerlordPlayerSettlement.Utils;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace BannerlordPlayerSettlement.Saves
{
    public class MetaV1_2
    {
        public Settlement? playerSettlement;

        public string displayName;
        public string identifier;
        public float buildTime;

        public string? savedModuleVersionRaw;
        public string savedModuleVersion;

        public int villageCount;
        public List<PlayerSettlementItem> villages = new();

        public static MetaV1_2? ReadFile(string userDir, string moduleName, ref string configDir)
        {
            var metaObj = new MetaV1_2();
            var metaFile = Path.Combine(configDir, $"meta.bin");
            if (File.Exists(metaFile))
            {
                string metaText = File.ReadAllText(metaFile);
                string originalMetaText = "" + metaText;

                var parts = metaText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                metaObj.identifier = parts[0].Base64Decode();
                metaObj.displayName = parts[1].Base64Decode();
                if (!float.TryParse(parts[2].Base64Decode(), out metaObj.buildTime) && !float.TryParse(parts[2], out metaObj.buildTime))
                {
                    LogManager.Log.NotifyBad("Unable to read save data!");
                    LogManager.EventTracer.Trace(@$"Unable to read save data!
    {metaText}
");
                    PlayerSettlementBehaviour.OldSaveLoaded = true;
                    return null;
                }
                metaObj.savedModuleVersionRaw = parts.Length > 3 ? parts[3] : null;
                metaObj.savedModuleVersion = parts.Length > 3 ? parts[3].Base64Decode() : "0.0.0";
                metaObj.villageCount = parts.Length > 4 ? int.TryParse(parts[4].Base64Decode(), out metaObj.villageCount) ? metaObj.villageCount : 0 : 0;
                if (metaObj.villageCount > 0 && parts.Length > 5)
                {
                    var villagesContents = parts[5].Base64Decode().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    for (int i = 0; i < villagesContents.Length; i++)
                    {
                        var villageParts = villagesContents[i].Base64Decode().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                        string vIdentifier = villageParts[0].Base64Decode();
                        string vName = villageParts[1].Base64Decode();
                        float vBuiltTime = float.Parse(villageParts[2].Base64Decode());

                        metaObj.villages.Add(new PlayerSettlementItem
                        {
                            ItemIdentifier = vIdentifier,
                            SettlementName = vName,
                            BuiltAt = vBuiltTime,

                        });
                    }
                }

                if (metaObj.savedModuleVersion != Main.Version)
                {
                    // TODO: Any version specific updates here
                    if (metaObj.savedModuleVersion == "1.0.0.0")
                    {
                        // Original version didnt split into separate campaign which caused save corruption.

                        if (metaObj.buildTime - 5 > Campaign.CurrentTime)
                        {
                            // A player settlement has been made in a different save.
                            // This is an older save than the config is for.
                            PlayerSettlementBehaviour.OldSaveLoaded = true;
                            return null;
                        }

                        PlayerSettlementBehaviour.UpdateUniqueGameId();
                        var oldConfigDir = configDir;
                        configDir = Path.Combine(userDir, "Configs", moduleName, Campaign.Current.UniqueGameId);
                        Directory.Move(oldConfigDir, configDir);

                        metaFile = Path.Combine(configDir, $"meta.bin");

                        PlayerSettlementBehaviour.TriggerSaveAfterUpgrade = true;
                    }

                    // Save with latest version to indicate compatibility
                    if (parts.Length > 3)
                    {
                        parts[3] = Main.Version.Base64Encode();
                        metaText = string.Join("\r\n", parts);
                    }
                    else
                    {
                        metaText += "\r\n";
                        // Line 4: Mod version
                        metaText += Main.Version.Base64Encode();
                    }
                    File.WriteAllText(metaFile + ".bak", originalMetaText);

                    File.WriteAllText(metaFile, metaText);

                    LogManager.Log.Print($"Updated {Main.DisplayName} to {Main.Version}", Colours.Purple);
                }


                metaObj.playerSettlement = MBObjectManager.Instance.GetObject<Settlement>(metaObj.identifier);

                return metaObj;
            }
            else
            {
                // No player settlement has been made
                return null;
            }
        }

        public MetaV3? Convert(string configDir)
        {
            var oldTownXML = Path.Combine(configDir, $"PlayerSettlement.xml");
            if (!File.Exists(oldTownXML))
            {
                return null;
            }

            var oldTownVillageXML1 = Path.Combine(configDir, $"PlayerSettlementVillage_1.xml");
            //var newTownVillageXML1 = Path.Combine(configDir, "PlayerTown_1_Village_1.xml");

            var oldTownVillageXML2 = Path.Combine(configDir, $"PlayerSettlementVillage_2.xml");
            //var newTownVillageXML2 = Path.Combine(configDir, "PlayerTown_1_Village_2.xml");

            var oldTownVillageXML3 = Path.Combine(configDir, $"PlayerSettlementVillage_3.xml");
            //var newTownVillageXML3 = Path.Combine(configDir, "PlayerTown_1_Village_3.xml");

            var metaV3 = new MetaV3()
            {
                SavedModuleVersion = Main.Version,
                Towns = new(),
                Castles = new(),
                ExtraVillages = new()
            };

            //if (playerSettlement != null)
            //{
            //    playerSettlement.StringId = "player_settlement_town_1";
            //}
            string townXml = File.ReadAllText(oldTownXML);
            metaV3.Towns.Add(new SettlementMetaV3
            {
                XML = townXml,
                BuildTime = buildTime,
                DisplayName = displayName,
                Identifier = int.Parse(identifier.Replace("player_settlement_town_", "")),
                settlement = playerSettlement,
                Villages = villages.Select((v, i) =>
                {
                    //if (v.Settlement != null)
                    //{
                    //    v.Settlement.StringId = $"player_settlement_town_1_village_{i + 1}";
                    //}
                    string? xml = null;
                    switch (i + 1)
                    {
                        case 1:
                            if (File.Exists(oldTownVillageXML1))
                            {
                                xml = File.ReadAllText(oldTownVillageXML1);
                                //contents = contents.Replace(identifier, "player_settlement_town_1");
                                //File.WriteAllText(newTownVillageXML1, contents);
                            }
                            break;
                        case 2:
                            if (File.Exists(oldTownVillageXML2))
                            {
                                xml = File.ReadAllText(oldTownVillageXML2);
                                //contents = contents.Replace(identifier, "player_settlement_town_1");
                                //File.WriteAllText(newTownVillageXML2, contents);
                            }
                            break;
                        case 3:
                            if (File.Exists(oldTownVillageXML3))
                            {
                                xml = File.ReadAllText(oldTownVillageXML3);
                                //contents = contents.Replace(identifier, "player_settlement_town_1");
                                //File.WriteAllText(newTownVillageXML3, contents);
                            }
                            break;
                    }
                    return new SettlementMetaV3
                    {
                        XML = xml,
                        BuildTime = v.BuiltAt,
                        DisplayName = v.SettlementName,
                        Identifier = i + 1,
                        settlement = v.Settlement,
                        // N/A
                        Villages = new()
                    };
                }).ToList()
            });

            //if (!metaV3.Save())
            //{
            //    return null;
            //}

            var metaFile = Path.Combine(configDir, $"meta.bin");
            if (File.Exists(metaFile))
            {
                if (File.Exists(metaFile + ".bak"))
                {
                    File.Delete(metaFile + ".bak");
                }
                File.Move(metaFile, metaFile + ".bak");
            }

            if (File.Exists(oldTownXML))
            {
                if (File.Exists(oldTownXML + ".bak"))
                {
                    File.Delete(oldTownXML + ".bak");
                }
                File.Move(oldTownXML, oldTownXML + ".bak");
            }
            if (File.Exists(oldTownVillageXML1))
            {
                if (File.Exists(oldTownVillageXML1 + ".bak"))
                {
                    File.Delete(oldTownVillageXML1 + ".bak");
                }
                File.Move(oldTownVillageXML1, oldTownVillageXML1 + ".bak");
            }
            if (File.Exists(oldTownVillageXML2))
            {
                if (File.Exists(oldTownVillageXML2 + ".bak"))
                {
                    File.Delete(oldTownVillageXML2 + ".bak");
                }
                File.Move(oldTownVillageXML2, oldTownVillageXML2 + ".bak");
            }
            if (File.Exists(oldTownVillageXML3))
            {
                if (File.Exists(oldTownVillageXML3 + ".bak"))
                {
                    File.Delete(oldTownVillageXML3 + ".bak");
                }
                File.Move(oldTownVillageXML3, oldTownVillageXML3 + ".bak");
            }

            PlayerSettlementBehaviour.TriggerSaveAfterUpgrade = true;
            return metaV3;
        }
    }
}
