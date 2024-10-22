
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace BannerlordPlayerSettlement.Saves
{
    public class Vec3Saveable
    {
        [SaveableField(401)]
        public float x;
        [SaveableField(402)]
        public float y;
        [SaveableField(403)]
        public float z;
        [SaveableField(404)]
        public float w;
        public Vec3Saveable()
        {

        }

        public Vec3Saveable(Vec3 source)
        {
            x = source.x;
            y = source.y;
            z = source.z;
            w = source.w;
        }

        public Vec3 ToVec3()
        {
            return new Vec3(x, y, z, w);
        }

        public static implicit operator Vec3(Vec3Saveable? source)
        {
            if (source == null)
            {
                return Vec3.Zero;
            }
            return source.ToVec3();
        }


        public static implicit operator Vec3Saveable?(Vec3? source)
        {
            if (source == null)
            {
                return null;
            }
            return new Vec3Saveable(source.Value);
        }
    }
}
