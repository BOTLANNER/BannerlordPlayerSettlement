using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace BannerlordPlayerSettlement.Saves
{
    public class Mat3Saveable
    {
        [SaveableField(301)]
        public float sx;
        [SaveableField(302)]
        public float sy;
        [SaveableField(303)]
        public float sz;
        [SaveableField(304)]
        public float fx;
        [SaveableField(305)]
        public float fy;
        [SaveableField(306)]
        public float fz;
        [SaveableField(307)]
        public float ux;
        [SaveableField(308)]
        public float uy;
        [SaveableField(309)]
        public float uz;

        public Mat3Saveable()
        {

        }

        public Mat3Saveable(Mat3 source)
        {
            sx = source.s.x;
            sy = source.s.y;
            sz = source.s.z;
            fx = source.f.x;
            fy = source.f.y;
            fz = source.f.z;
            ux = source.u.x;
            uy = source.u.y;
            uz = source.u.z;
        }

        public Mat3 ToMat3()
        {
            return new Mat3(sx, sy, sz, fx, fy, fz, ux, uy, uz);
        }

        public static implicit operator Mat3(Mat3Saveable source)
        {
            return source.ToMat3();
        }


        public static implicit operator Mat3Saveable?(Mat3? source)
        {
            if (source == null)
            {
                return null;
            }
            return new Mat3Saveable(source.Value);
        }
    }
}
