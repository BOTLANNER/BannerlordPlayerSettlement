using TaleWorlds.SaveSystem;

namespace BannerlordPlayerSettlement.Saves
{
    public class DeepTransformEdit
    {
        [SaveableField(601)]
        public int Index = -1;

        [SaveableField(602)]
        public string Name = "";

        [SaveableField(603)]
        public TransformSaveable? Transform;

        [SaveableField(604)]
        public bool IsDeleted;
    }
}
