using System.Collections.Generic;
using System.Xml;

using Newtonsoft.Json;

using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace BannerlordPlayerSettlement.Saves
{
    [JsonObject(MemberSerialization.OptIn)]
    public class SettlementMetaV3
    {
        public Settlement? settlement;

        private XmlDocument? _doc = null;
        public XmlDocument Document
        {
            get
            {
                if (_doc == null)
                {
                    _doc = new XmlDocument();
                    _doc.LoadXml(XML);
                }
                return _doc;
            }
        }

        [JsonProperty]
        [SaveableProperty(331)]
        public string XML { get; set; }

        [JsonProperty]
        [SaveableProperty(332)]
        public string DisplayName { get; set; }

        [JsonProperty]
        [SaveableProperty(333)]
        public int Identifier { get; set; }

        [JsonProperty]
        [SaveableProperty(334)]
        public float BuildTime { get; set; }

        [JsonProperty]
        [SaveableProperty(335)]
        public List<SettlementMetaV3> Villages { get; set; } = new();

    }
}
