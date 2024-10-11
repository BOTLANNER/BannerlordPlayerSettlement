using System.Xml;

namespace BannerlordPlayerSettlement.Descriptors
{
    public class PlayerSettlementItemTemplate
    {
        public XmlNode ItemXML;

        public string Id;

        public string Culture;

        public int Variant;

        public int Type = 0;
    }
}
