using TaleWorlds.SaveSystem;

namespace BannerlordPlayerSettlement.Saves
{
    public class TransformSaveable
    {
        [SaveableField(502)]
        public Mat3Saveable? RotationScale;
        [SaveableField(503)]
        public Vec3Saveable? Position;
        [SaveableField(504)]
        public Vec3Saveable? Offsets;
    }
}
