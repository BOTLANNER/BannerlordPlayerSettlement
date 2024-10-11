using System.Xml;

namespace BannerlordPlayerSettlement.Descriptors
{
    public class CultureSettlementTemplate
    {
        public string FromModule;
        public string TemplateModifier;

        public XmlDocument Document;
        public string CultureId;

        public int Castles;
        public int Towns;
        public int Villages;
    }
}
