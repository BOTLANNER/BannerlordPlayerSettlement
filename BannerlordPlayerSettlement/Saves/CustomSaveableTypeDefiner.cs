﻿using System.Collections.Generic;

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

            AddClassDefinition(typeof(Mat3Saveable), 9);
            AddClassDefinition(typeof(Vec3Saveable), 10);
            AddClassDefinition(typeof(TransformSaveable), 11);
            AddClassDefinition(typeof(DeepTransformEdit), 12);
            AddClassDefinition(typeof(PlayerSettlementItem), 3);
            AddClassDefinition(typeof(OverwriteSettlementItem), 13);
            AddClassDefinition(typeof(PlayerSettlementInfo), 2);

            AddClassDefinition(typeof(SettlementMetaV3), 5);
            AddClassDefinition(typeof(MetaV3), 7);
        }

        protected override void DefineContainerDefinitions()
        {
            base.DefineContainerDefinitions();

            ConstructContainerDefinition(typeof(List<DeepTransformEdit>));
            ConstructContainerDefinition(typeof(DeepTransformEdit[]));

            ConstructContainerDefinition(typeof(List<PlayerSettlementItem>));
            ConstructContainerDefinition(typeof(PlayerSettlementItem[]));

            ConstructContainerDefinition(typeof(List<OverwriteSettlementItem>));
            ConstructContainerDefinition(typeof(OverwriteSettlementItem[]));

            ConstructContainerDefinition(typeof(List<SettlementMetaV3>));
            ConstructContainerDefinition(typeof(SettlementMetaV3[]));
        }
    }
}
