using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace BannerlordPlayerSettlement.Saves
{
    internal sealed class CustomSaveableTypeDefiner : SaveableTypeDefiner
    {
        public const int SaveBaseId_b0tlanner0 = 300_711_200;
        public const int SaveBaseId = SaveBaseId_b0tlanner0 + 4;

        public CustomSaveableTypeDefiner() : base(SaveBaseId) { }

        protected override void DefineClassTypes()
        {
            base.DefineClassTypes();

            AddClassDefinition(typeof(PlayerSettlementItem), 3);
            AddClassDefinition(typeof(PlayerSettlementInfo), 2);
        }

        protected override void DefineContainerDefinitions()
        {
            base.DefineContainerDefinitions();

            ConstructContainerDefinition(typeof(List<PlayerSettlementItem>));
            ConstructContainerDefinition(typeof(PlayerSettlementItem[]));
        }
    }
}
