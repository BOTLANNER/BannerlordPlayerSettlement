using System;

using TaleWorlds.InputSystem;

namespace BannerlordPlayerSettlement.Extensions
{
    public static class InputContextExtensions
    {
        public static InputKey GetKey(string toUse, InputKey defaultKey)
        {
            InputKey key;
            try
            {
                toUse = toUse.Length == 1 ? toUse.ToUpper() : toUse;
                key = (InputKey) Enum.Parse(typeof(InputKey), toUse);
            }
            catch (Exception) { return defaultKey; }
            return key;
        }

        public static InputKey GetKeyFrom(this IInputContext inputContext, string toUse, InputKey defaultKey)
        {
            return GetKey(toUse, defaultKey);
        }

        public static InputKey GetKeyFrom(this IInputManager inputMan, string toUse, InputKey defaultKey)
        {
            return GetKey(toUse, defaultKey);
        }
    }
}
