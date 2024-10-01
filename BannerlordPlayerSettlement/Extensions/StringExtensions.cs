using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BannerlordPlayerSettlement.Extensions
{
    public static class StringExtensions
    {
        public static string Base64Encode(this string plainText, bool silent = true)
        {
            try
            {
                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
                return System.Convert.ToBase64String(plainTextBytes);
            }
            catch (Exception)
            {
                if (silent)
                {
                    return plainText;
                }
                throw;
            }
        }

        public static string Base64Decode(this string base64EncodedData, bool silent = true)
        {
            try
            {
                var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
                return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
            }
            catch (Exception)
            {
                if (silent)
                {
                    return base64EncodedData;
                }
                throw;
            }
        }
    }
}
