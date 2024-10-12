
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.Utils
{
    public static class Colours
    {
        public static Color ImportantTextColor => Color.FromUint(0x00F16D26); // orange

        public static  Color Error => new(178 * 255, 34 * 255, 34 * 255);
        public static  Color Warn => new(189 * 255, 38 * 255, 0);

        public static Color Aqua => new(0f, 1f, 1f, 1f);

        public static Color Black => new(0f, 0f, 0f, 1f);

        public static Color Blue => new(0f, 0f, 1f, 1f);

        public static Color Gray => new(0.5f, 0.5f, 0.5f, 1f);

        public static Color Green => new(0f, 1f, 0f, 1f);

        public static Color LimeGreen => new(0.5f, 1f, 0f, 1f);

        public static Color Orange => new(1f, 0.5f, 0f, 1f);

        public static Color Purple => new(1f, 0f, 1f, 1f);

        public static Color Red => new(1f, 0f, 0f, 1f);

        public static Color SkyBlue => new(0f, 0.5f, 1f, 1f);

        public static Color White => new(1f, 1f, 1f, 1f);

        public static Color Yellow => new(1f, 1f, 0f, 1f);
        public static Color Magenta => Color.FromUint(0x00FF007F);
        public static Color ForestGreen => Color.FromUint(0x00FF007F);
    }
}
