using System;
using System.IO;
using System.Xml;

namespace PlayerSettlementPrefabGenerator
{
    class Program
    {
        internal static string PlayerSettlementTownPrefabTemplate = LoadResourceAsXML("PlayerSettlementPrefabGenerator.player_settlements_prefabs_town_template.xml").OuterXml;
        internal static string PlayerSettlementCastlePrefabTemplate = LoadResourceAsXML("PlayerSettlementPrefabGenerator.player_settlements_prefabs_castle_template.xml").OuterXml;
        internal static string PlayerSettlementVillage1PrefabTemplate = LoadResourceAsXML("PlayerSettlementPrefabGenerator.player_settlements_prefabs_village_template_1.xml").OuterXml;
        internal static string PlayerSettlementVillage2PrefabTemplate = LoadResourceAsXML("PlayerSettlementPrefabGenerator.player_settlements_prefabs_village_template_2.xml").OuterXml;
        internal static string PlayerSettlementVillage3PrefabTemplate = LoadResourceAsXML("PlayerSettlementPrefabGenerator.player_settlements_prefabs_village_template_3.xml").OuterXml;
        static void Main(string[] args)
        {
            string allPrefabs = "";

            int townCount = int.Parse(args[0]);
            int villagePerTownCount = int.Parse(args[1]);
            int castleCount = int.Parse(args[2]);
            int villagePerCastleCount = int.Parse(args[3]);

            Console.WriteLine($"Creating {townCount} Towns with {villagePerCastleCount}");
            Console.WriteLine($"Creating {castleCount} Castles with {villagePerCastleCount}");

            Random rnd = new Random();
            for (int t = 0; t < townCount; t++)
            {
                var townNumber = t + 1;
                Console.WriteLine($"Creating Town {townNumber}");

                allPrefabs += $"\r\n{PlayerSettlementTownPrefabTemplate.Replace("{{X}}", townNumber.ToString())}";

                for (int v = 0; v < villagePerTownCount; v++)
                {
                    var villageNumber = v + 1;
                    Console.WriteLine($"Creating Town {townNumber} Village {villageNumber}");
                    var villageTemplateNumber = rnd.Next(1, 4);
                    var template = villageTemplateNumber == 1 ? PlayerSettlementVillage1PrefabTemplate : villageTemplateNumber == 2 ? PlayerSettlementVillage2PrefabTemplate : PlayerSettlementVillage3PrefabTemplate;

                    allPrefabs += $"\r\n{template.Replace("{{owner}}", "town").Replace("{{X}}", townNumber.ToString()).Replace("{{Y}}", villageNumber.ToString())}";
                }
            }
            for (int c = 0; c < castleCount; c++)
            {
                var castleNumber = c + 1;
                Console.WriteLine($"Creating Castle {castleNumber}");

                allPrefabs += $"\r\n{PlayerSettlementCastlePrefabTemplate.Replace("{{X}}", castleNumber.ToString())}";

                for (int v = 0; v < villagePerCastleCount; v++)
                {
                    var villageNumber = v + 1;
                    Console.WriteLine($"Creating Castle {castleNumber} Village {villageNumber}");
                    var villageTemplateNumber = rnd.Next(1, 4);
                    var template = villageTemplateNumber == 1 ? PlayerSettlementVillage1PrefabTemplate : villageTemplateNumber == 2 ? PlayerSettlementVillage2PrefabTemplate : PlayerSettlementVillage3PrefabTemplate;

                    allPrefabs += $"\r\n{template.Replace("{{owner}}", "castle").Replace("{{X}}", castleNumber.ToString()).Replace("{{Y}}", villageNumber.ToString())}";
                }
            }

            var xml = @$"
<prefabs>
    {allPrefabs}
</prefabs>
";
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            xml = doc.OuterXml;

            File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "staged.xml"), xml);

            Console.WriteLine($"Creating {townCount} Towns with {villagePerCastleCount}");
            Console.WriteLine($"Creating {castleCount} Castles with {villagePerCastleCount}");
            Console.WriteLine($"Press enter...");
            Console.ReadLine();
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
