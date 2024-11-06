using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using System.Linq;

namespace PlayerSettlementPrefabGenerator
{
    class Program
    {
        private static int townCount;
        private static int villagePerTownCount;
        private static int castleCount;
        private static int villagePerCastleCount;
        private static string modifier;

        static void Main(string[] args)
        {
            Console.Clear();

            string settlementsDescriptorsFiles = (args[0]).Replace("$(BANNERLORD_GAME_DIR)", Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR"))
                                                            .Replace("$(BANNERLORD_WORKSHOP_DIR)", Environment.GetEnvironmentVariable("BANNERLORD_WORKSHOP_DIR"));
            string entityDescriptorsFiles = (args[1]).Replace("$(BANNERLORD_GAME_DIR)", Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR"))
                                                        .Replace("$(BANNERLORD_WORKSHOP_DIR)", Environment.GetEnvironmentVariable("BANNERLORD_WORKSHOP_DIR"));

            townCount = int.Parse(args[2]);
            villagePerTownCount = int.Parse(args[3]);
            castleCount = int.Parse(args[4]);
            villagePerCastleCount = int.Parse(args[5]);

            modifier = "";
            if (args.Length > 6)
            {
                modifier = args[6];
            }

            bool prependGameDir = !args.Contains("--no-prepend");

            List<XmlDocument> settlementsDescriptors = new();
            foreach (var item in settlementsDescriptorsFiles.Split("|"))
            {
                string settlementsDescriptorsFile = item;
                if (prependGameDir &&!settlementsDescriptorsFile.StartsWith(Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR")))
                {
                    settlementsDescriptorsFile = Path.Combine(Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR"), settlementsDescriptorsFile);
                }
                XmlDocument settlementsDescriptor = new XmlDocument();
                settlementsDescriptor.Load(settlementsDescriptorsFile);
                settlementsDescriptors.Add(settlementsDescriptor);
            }

            List<XmlDocument> entityDescriptors = new();
            foreach (var item in entityDescriptorsFiles.Split("|"))
            {
                string entityDescriptorsFile = item;
                if (prependGameDir && !entityDescriptorsFile.StartsWith(Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR")))
                {
                    entityDescriptorsFile = Path.Combine(Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR"), entityDescriptorsFile);
                }
                XmlDocument entityDescriptor = new XmlDocument();
                entityDescriptor.Load(entityDescriptorsFile);
                entityDescriptors.Add(entityDescriptor);
            }


            SplitCulturesXml(settlementsDescriptors, entityDescriptors, out Dictionary<string, CultureSettlementInfo> infos);

            var outDir = Path.Combine(Environment.CurrentDirectory, "Out");

            if (Directory.Exists(outDir))
            {
                Directory.Delete(outDir, true);
            }

            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            var prefabsOutDir = Path.Combine(outDir, "Prefabs");

            if (!Directory.Exists(prefabsOutDir))
            {
                Directory.CreateDirectory(prefabsOutDir);
            }

            var templatesOutDir = Path.Combine(outDir, "ModuleData", "Player_Settlement_Templates");

            if (!Directory.Exists(templatesOutDir))
            {
                Directory.CreateDirectory(templatesOutDir);
            }

            foreach (var item in infos)
            {
                var root = item.Value.Settlements.ChildNodes[0];

                XmlAttribute castles = item.Value.Settlements.CreateAttribute("castles");
                castles.Value = item.Value.CastleCount.ToString();
                root.Attributes.SetNamedItem(castles);

                XmlAttribute towns = item.Value.Settlements.CreateAttribute("towns");
                towns.Value = item.Value.TownCount.ToString();
                root.Attributes.SetNamedItem(towns);

                XmlAttribute villages = item.Value.Settlements.CreateAttribute("villages");
                villages.Value = item.Value.VillageCount.ToString();
                root.Attributes.SetNamedItem(villages);

                XmlAttribute villagesPerCastle = item.Value.Settlements.CreateAttribute("min_castle_village_prefabs");
                villagesPerCastle.Value = villagePerCastleCount.ToString();
                root.Attributes.SetNamedItem(villagesPerCastle);

                XmlAttribute villagesPerTown = item.Value.Settlements.CreateAttribute("min_town_village_prefabs");
                villagesPerTown.Value = villagePerTownCount.ToString();
                root.Attributes.SetNamedItem(villagesPerTown);

                XmlAttribute cultureTemplate = item.Value.Settlements.CreateAttribute("culture_template");
                cultureTemplate.Value = item.Value.CultureId;
                root.Attributes.SetNamedItem(cultureTemplate);

                XmlAttribute templateModifier = item.Value.Settlements.CreateAttribute("template_modifier");
                templateModifier.Value = modifier;
                root.Attributes.SetNamedItem(templateModifier);

                File.WriteAllText(Path.Combine(templatesOutDir, $"{item.Key}_settlements_templates{modifier}.xml"), item.Value.Settlements.OuterXml);

                File.WriteAllText(Path.Combine(prefabsOutDir, $"{item.Key}_player_settlements_town_prefabs{modifier}.xml"), item.Value.TownPrefabs.OuterXml);
                File.WriteAllText(Path.Combine(prefabsOutDir, $"{item.Key}_player_settlements_town_village_prefabs{modifier}.xml"), item.Value.TownVillagePrefabs.OuterXml);
                File.WriteAllText(Path.Combine(prefabsOutDir, $"{item.Key}_player_settlements_castle_prefabs{modifier}.xml"), item.Value.CastlePrefabs.OuterXml);
                File.WriteAllText(Path.Combine(prefabsOutDir, $"{item.Key}_player_settlements_castle_village_prefabs{modifier}.xml"), item.Value.CastleVillagePrefabs.OuterXml);
            }

            Console.WriteLine();
            Console.WriteLine($"Output at '{new DirectoryInfo(outDir).FullName}'");
            Console.WriteLine($"Press enter...");
            Console.ReadLine();
        }

        public static void SplitCulturesXml(List<XmlDocument> settlementsDocs, List<XmlDocument> prefabsDocs, out Dictionary<string, CultureSettlementInfo> infos)
        {
            infos = new Dictionary<string, CultureSettlementInfo>();
            int num = 0;
            bool flag = false;
            string elementName = "Settlements";
            foreach (var settlementsDoc in settlementsDocs)
            {
                while (num < settlementsDoc.ChildNodes.Count)
                {
                    int num1 = num;
                    if (elementName == settlementsDoc.ChildNodes[num1].Name)
                    {
                        flag = true;
                        break;
                    }
                    num++;
                }
                if (flag)
                {
                    string lastCulture = null;
                    for (XmlNode i = settlementsDoc.ChildNodes[num].ChildNodes[0]; i != null; i = i.NextSibling)
                    {
                        if (i.NodeType != XmlNodeType.Comment && i.Name == "Settlement")
                        {
                            string id = i.Attributes?["id"]?.Value;
                            var prefab = prefabsDocs.Select(d => d.SelectSingleNode($"//*[@name='{id}']")).FirstOrDefault(n => n != null);
                            if (prefab == null)
                            {
                                Console.WriteLine($"\nMissing prefab {id}! Skipped...");
                                continue;
                            }

                            if (!string.IsNullOrEmpty(i.Attributes?["text"]?.Value))
                            {
                                i.Attributes.Remove(i.Attributes["text"]);
                            }

                            string culture = i.Attributes?["culture"]?.Value;
                            if (culture.StartsWith("Culture."))
                            {
                                culture = culture.Substring(8);
                            }
                            bool isVillage = false;
                            bool isCastle = false;
                            bool isTown = false;

                            XmlNode componentNode = null;

                            foreach (XmlNode childNode in i.ChildNodes)
                            {
                                if (childNode.Name == "Components")
                                {
                                    foreach (XmlNode xmlNodes in childNode.ChildNodes)
                                    {
                                        componentNode = xmlNodes;
                                        var component = xmlNodes.Name;
                                        if (component == "Village")
                                        {
                                            isVillage = true;
                                            break;
                                        }
                                        else if (component == "Town")
                                        {
                                            // Castles use the Town component
                                            isCastle = (xmlNodes.Attributes["is_castle"] == null ? false : Boolean.Parse(xmlNodes.Attributes["is_castle"].Value));
                                            isTown = !isCastle;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (!isCastle && !isVillage && !isTown)
                            {
                                Console.WriteLine($"\nUnknown component type {id} - {componentNode?.Name}! Skipped...");
                                continue;
                            }

                            if (!infos.ContainsKey(culture))
                            {
                                var info0 = new CultureSettlementInfo
                                {
                                    CultureId = culture,
                                    Settlements = new XmlDocument(),
                                    TownPrefabs = new XmlDocument(),
                                    TownVillagePrefabs = new XmlDocument(),
                                    CastlePrefabs = new XmlDocument(),
                                    CastleVillagePrefabs = new XmlDocument(),
                                };
                                info0.Settlements.LoadXml("<Settlements></Settlements>");
                                info0.TownPrefabs.LoadXml("<prefabs></prefabs>");
                                info0.TownVillagePrefabs.LoadXml("<prefabs></prefabs>");
                                info0.CastlePrefabs.LoadXml("<prefabs></prefabs>");
                                info0.CastleVillagePrefabs.LoadXml("<prefabs></prefabs>");
                                infos[culture] = info0;
                            }

                            var info = infos[culture];


                            string newId;

                            string notify = "";
                            if (isVillage)
                            {
                                info.VillageCount = info.VillageCount + 1;
                                newId = "player_settlement_{{OWNER_TYPE}}_village" + $"_{culture}_variant_{info.VillageCount}{modifier}";
                                notify = ($"{culture} {info.VillageCount} village variant");
                            }
                            else if (isCastle)
                            {
                                info.CastleCount = info.CastleCount + 1;
                                newId = "player_settlement_castle" + $"_{culture}_variant_{info.CastleCount}{modifier}";
                                notify = ($"{culture} {info.CastleCount} castle variant");
                            }
                            else
                            {
                                info.TownCount = info.TownCount + 1;
                                newId = "player_settlement_town" + $"_{culture}_variant_{info.TownCount}{modifier}";
                                notify = ($"{culture} {info.TownCount} town variant");
                            }

                            if (lastCulture != culture)
                            {
                                Console.WriteLine();
                            }
                            else
                            {
                                Console.CursorLeft = 0;
                            }
                            Console.Write(notify + "                                          ");

                            lastCulture = culture;

                            i.Attributes["id"].Value = newId;
                            i.Attributes["name"].Value = "{=player_settlement_n_01}Player Settlement";
                            i.Attributes["posX"].Value = "{{POS_X}}";
                            i.Attributes["posY"].Value = "{{POS_Y}}";
                            i.Attributes["culture"].Value = "Culture.{{PLAYER_CULTURE}}";
                            if (!isVillage)
                            {
                                i.Attributes["owner"].Value = "Faction.{{PLAYER_CLAN}}";
                                if (i.Attributes["gate_posX"] != null)
                                {
                                    i.Attributes["gate_posX"].Value = "{{G_POS_X}}";
                                }
                                if (i.Attributes["gate_posY"] != null)
                                {
                                    i.Attributes["gate_posY"].Value = "{{G_POS_Y}}";
                                }
                            }

                            if (componentNode != null)
                            {
                                var componentId = componentNode.Attributes["id"].Value;
                                string newCompId;
                                if (isVillage)
                                {
                                    newCompId = "player_settlement_{{OWNER_TYPE}}_village_comp" + $"_{culture}_variant_{info.VillageCount}{modifier}";
                                }
                                else if (isCastle)
                                {
                                    newCompId = "player_settlement_castle_castle_comp" + $"_{culture}_variant_{info.CastleCount}{modifier}";
                                }
                                else
                                {
                                    newCompId = "player_settlement_town_town_comp" + $"_{culture}_variant_{info.TownCount}{modifier}";
                                }
                                componentNode.Attributes["id"].Value = newCompId;
                                if (isVillage)
                                {
                                    componentNode.Attributes["village_type"].Value = "VillageType.{{VILLAGE_TYPE}}";
                                    // bound is a placeholder and will be updated at build time
                                    componentNode.Attributes["bound"].Value = "Settlement.player_settlement_{{OWNER_TYPE}}" + $"_{culture}_variant_{{{{OWNER_COUNT}}}}{modifier}";
                                }
                            }

                            var newNode = info.Settlements.ImportNode(i, true);

                            XmlAttribute templateType = info.Settlements.CreateAttribute("template_type");
                            templateType.Value = isVillage ? "Village" : isCastle ? "Castle" : "Town";
                            newNode.Attributes.SetNamedItem(templateType);

                            XmlAttribute templateVariant = info.Settlements.CreateAttribute("template_variant");
                            templateVariant.Value = isVillage ? info.VillageCount.ToString() : isCastle ? info.CastleCount.ToString() : info.TownCount.ToString();
                            newNode.Attributes.SetNamedItem(templateVariant);

                            info.Settlements.ChildNodes[0].AppendChild(newNode);

                            if (isVillage)
                            {
                                //for (int c = 0; c < castleCount; c++)
                                //{
                                //    for (int v = 0; v < villagePerCastleCount; v++)
                                {
                                    Console.CursorLeft = 0;
                                    //Console.Write($"{notify} prefab {v + 1} for castle {c + 1}                                       ");
                                    Console.Write($"{notify} prefab for castle village                                        ");
                                    var newPrefab = info.CastleVillagePrefabs.ImportNode(prefab.CloneNode(true), true);
                                    newPrefab.Attributes["name"].Value = newId
                                        .Replace("{{OWNER_TYPE}}", "castle");
                                    //.Replace("{{BASE_IDENTIFIER}}", (c + 1).ToString())
                                    //.Replace("{{VILLAGE_NUMBER}}", (v + 1).ToString());

                                    XmlAttribute prefabTemplateType = info.CastleVillagePrefabs.CreateAttribute("template_type");
                                    prefabTemplateType.Value = "Castle_Village";
                                    newPrefab.Attributes.SetNamedItem(templateType);

                                    info.CastleVillagePrefabs.ChildNodes[0].AppendChild(newPrefab);

                                }
                                //}
                                //for (int t = 0; t < townCount; t++)
                                //{
                                //    for (int v = 0; v < villagePerTownCount; v++)
                                {
                                    Console.CursorLeft = 0;
                                    //Console.Write($"{notify} prefab {v + 1} for town {t + 1}                                         ");
                                    Console.Write($"{notify} prefab for town village                                         ");
                                    var newPrefab = info.TownVillagePrefabs.ImportNode(prefab.CloneNode(true), true);
                                    newPrefab.Attributes["name"].Value = newId
                                        .Replace("{{OWNER_TYPE}}", "town");
                                    //.Replace("{{BASE_IDENTIFIER}}", (t + 1).ToString())
                                    //.Replace("{{VILLAGE_NUMBER}}", (v + 1).ToString());

                                    XmlAttribute prefabTemplateType = info.TownVillagePrefabs.CreateAttribute("template_type");
                                    prefabTemplateType.Value = "Town_Village";
                                    newPrefab.Attributes.SetNamedItem(templateType);

                                    info.TownVillagePrefabs.ChildNodes[0].AppendChild(newPrefab);

                                }
                                //}
                            }
                            else if (isCastle)
                            {
                                //for (int c = 0; c < castleCount; c++)
                                {
                                    Console.CursorLeft = 0;
                                    //Console.Write($"{notify} prefab {c + 1}                                                                                 ");
                                    Console.Write($"{notify} prefab castle                                                     ");
                                    var newPrefab = info.CastlePrefabs.ImportNode(prefab.CloneNode(true), true);
                                    newPrefab.Attributes["name"].Value = newId;
                                    //.Replace("{{BASE_IDENTIFIER}}", (c + 1).ToString());

                                    XmlAttribute prefabTemplateType = info.CastlePrefabs.CreateAttribute("template_type");
                                    prefabTemplateType.Value = "Castle";
                                    newPrefab.Attributes.SetNamedItem(templateType);

                                    info.CastlePrefabs.ChildNodes[0].AppendChild(newPrefab);
                                }
                            }
                            else
                            {
                                //for (int t = 0; t < townCount; t++)
                                {
                                    Console.CursorLeft = 0;
                                    //Console.Write($"{notify} prefab {t + 1}                                                                                 ");
                                    Console.Write($"{notify} prefab town                                                       ");
                                    var newPrefab = info.TownPrefabs.ImportNode(prefab.CloneNode(true), true);
                                    newPrefab.Attributes["name"].Value = newId;
                                    //.Replace("{{BASE_IDENTIFIER}}", (t + 1).ToString());

                                    XmlAttribute prefabTemplateType = info.TownPrefabs.CreateAttribute("template_type");
                                    prefabTemplateType.Value = "Town";
                                    newPrefab.Attributes.SetNamedItem(templateType);

                                    info.TownPrefabs.ChildNodes[0].AppendChild(newPrefab);
                                }
                            }
                        }
                    }

                    Random random = new Random();
                    foreach (var (culture, settlementInfo) in infos)
                    {
                        {
                            var castlesRoot = settlementInfo.CastlePrefabs.ChildNodes[0];
                            Console.WriteLine();
                            while (settlementInfo.CastleCount < castleCount)
                            {
                                if (settlementInfo.CastleCount == 0)
                                {
                                    break;
                                }
                                settlementInfo.CastleCount = settlementInfo.CastleCount + 1;

                                var notify = ($"{culture} {settlementInfo.CastleCount} castle variant random");
                                Console.CursorLeft = 0;
                                Console.Write($"{notify} prefab for castle                                           ");

                                string newId = "player_settlement_castle" + $"_{culture}_variant_{settlementInfo.CastleCount}{modifier}";
                                string newCompId = "player_settlement_castle_castle_comp" + $"_{culture}_variant_{settlementInfo.CastleCount}{modifier}";

                                var randomNodeIdx = random.Next(0, castlesRoot.ChildNodes.Count);

                                var newPrefab = settlementInfo.CastlePrefabs.ImportNode(castlesRoot.ChildNodes[randomNodeIdx].CloneNode(true), true);
                                newPrefab.Attributes["name"].Value = newId;

                                castlesRoot.AppendChild(newPrefab);

                                var settlementNodes = settlementInfo.Settlements.SelectNodes($"descendant::Settlement[@template_type='Castle']");
                                // [@template_variant='{randomNodeIdx + 1}']
                                var settlementNode = settlementNodes.OfType<XmlNode>().FirstOrDefault(n => n.Attributes["template_variant"].Value == $"{randomNodeIdx + 1}");

                                var newNode = settlementInfo.Settlements.ImportNode(settlementNode, true);
                                newNode.Attributes["template_variant"].Value = settlementInfo.CastleCount.ToString();
                                newNode.Attributes["id"].Value = newId;

                                // Castles use the Town component
                                var newNodeComponent = newNode.SelectSingleNode("descendant::Town");
                                newNodeComponent.Attributes["id"].Value = newCompId;

                                settlementInfo.Settlements.ChildNodes[0].AppendChild(newNode);

                            }
                        }
                        {
                            var townsRoot = settlementInfo.TownPrefabs.ChildNodes[0];
                            Console.WriteLine();
                            while (settlementInfo.TownCount < townCount)
                            {
                                if (settlementInfo.TownCount == 0)
                                {
                                    break;
                                }
                                settlementInfo.TownCount = settlementInfo.TownCount + 1;

                                var notify = ($"{culture} {settlementInfo.TownCount} town variant random");
                                Console.CursorLeft = 0;
                                Console.Write($"{notify} prefab for town                                           ");

                                string newId = "player_settlement_town" + $"_{culture}_variant_{settlementInfo.TownCount}{modifier}";
                                string newCompId = "player_settlement_town_town_comp" + $"_{culture}_variant_{settlementInfo.TownCount}{modifier}";

                                var randomNodeIdx = random.Next(0, townsRoot.ChildNodes.Count);

                                var newPrefab = settlementInfo.TownPrefabs.ImportNode(townsRoot.ChildNodes[randomNodeIdx].CloneNode(true), true);
                                newPrefab.Attributes["name"].Value = newId;

                                townsRoot.AppendChild(newPrefab);

                                var settlementNodes = settlementInfo.Settlements.SelectNodes($"descendant::Settlement[@template_type='Town']");
                                // [@template_variant='{randomNodeIdx + 1}']
                                var settlementNode = settlementNodes.OfType<XmlNode>().FirstOrDefault(n => n.Attributes["template_variant"].Value == $"{randomNodeIdx + 1}");

                                var newNode = settlementInfo.Settlements.ImportNode(settlementNode, true);
                                newNode.Attributes["template_variant"].Value = settlementInfo.TownCount.ToString();
                                newNode.Attributes["id"].Value = newId;

                                var newNodeComponent = newNode.SelectSingleNode("descendant::Town");
                                newNodeComponent.Attributes["id"].Value = newCompId;

                                settlementInfo.Settlements.ChildNodes[0].AppendChild(newNode);
                            }
                        }
                        {
                            var castleVillagesRoot = settlementInfo.CastleVillagePrefabs.ChildNodes[0];
                            var townVillagesRoot = settlementInfo.TownVillagePrefabs.ChildNodes[0];
                            Console.WriteLine();
                            // Need enough villages to cater for all towns and castles
                            while (settlementInfo.VillageCount < (villagePerCastleCount + villagePerTownCount))
                            {
                                if (settlementInfo.VillageCount == 0)
                                {
                                    break;
                                }
                                settlementInfo.VillageCount = settlementInfo.VillageCount + 1;

                                var notify = ($"{culture} {settlementInfo.VillageCount} village variant random");
                                Console.CursorLeft = 0;
                                Console.Write($"{notify} prefab for castle and town                                ");

                                // Indexes should match
                                var randomNodeIdx = random.Next(0, castleVillagesRoot.ChildNodes.Count);

                                //if (settlementInfo.villageCount < villagePerCastleCount)
                                {
                                    string newId = "player_settlement_castle_village" + $"_{culture}_variant_{settlementInfo.VillageCount}{modifier}";

                                    //var randomNodeIdx = random.Next(0, castleVillagesRoot.ChildNodes.Count);

                                    var newPrefab = settlementInfo.CastleVillagePrefabs.ImportNode(castleVillagesRoot.ChildNodes[randomNodeIdx].CloneNode(true), true);
                                    newPrefab.Attributes["name"].Value = newId;

                                    castleVillagesRoot.AppendChild(newPrefab);
                                }

                                //if (settlementInfo.villageCount < villagePerTownCount)
                                {
                                    string newId = "player_settlement_town_village" + $"_{culture}_variant_{settlementInfo.VillageCount}{modifier}";

                                    //var randomNodeIdx = random.Next(0, townVillagesRoot.ChildNodes.Count);

                                    var newPrefab = settlementInfo.TownVillagePrefabs.ImportNode(townVillagesRoot.ChildNodes[randomNodeIdx].CloneNode(true), true);
                                    newPrefab.Attributes["name"].Value = newId;

                                    townVillagesRoot.AppendChild(newPrefab);
                                }

                                string newId2 = "player_settlement_{{OWNER_TYPE}}_village" + $"_{culture}_variant_{settlementInfo.VillageCount}{modifier}";
                                string newCompId = "player_settlement_{{OWNER_TYPE}}_village_comp" + $"_{culture}_variant_{settlementInfo.VillageCount}{modifier}";

                                var settlementNodes = settlementInfo.Settlements.SelectNodes($"descendant::Settlement[@template_type='Village']");
                                // [@template_variant='{randomNodeIdx + 1}']
                                var settlementNode = settlementNodes.OfType<XmlNode>().FirstOrDefault(n => n.Attributes["template_variant"].Value == $"{randomNodeIdx + 1}");

                                var newNode = settlementInfo.Settlements.ImportNode(settlementNode, true);
                                newNode.Attributes["template_variant"].Value = settlementInfo.VillageCount.ToString();
                                newNode.Attributes["id"].Value = newId2;

                                var newNodeComponent = newNode.SelectSingleNode("descendant::Village");
                                newNodeComponent.Attributes["id"].Value = newCompId;

                                settlementInfo.Settlements.ChildNodes[0].AppendChild(newNode);
                            }
                        }
                    }
                }
            }
        }

        public static XmlDocument LoadResourceAsXML(string embedPath)
        {
            using var stream = typeof(Program).Assembly.GetManifestResourceStream(embedPath);
            if (stream is null)
                throw new NullReferenceException($"Could not find embed resource '{embedPath}'!");
            using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreComments = true });
            var doc = new XmlDocument();
            doc.Load(xmlReader);
            return doc;
        }
    }
}
