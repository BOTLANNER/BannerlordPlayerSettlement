using System.Xml;

namespace PlayerSettlementPrefabGenerator
{
    public class CultureSettlementInfo
    {
        public XmlDocument Settlements;

        public XmlDocument TownPrefabs;
        public XmlDocument TownVillagePrefabs;
        public XmlDocument CastlePrefabs;
        public XmlDocument CastleVillagePrefabs;

        public string CultureId;
        public int TownCount;
        public int CastleCount;
        public int VillageCount;
    }
}
