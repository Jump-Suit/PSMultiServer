using System.Text;

namespace MultiSocks.Utils
{
    public class LobbyTagField
    {
        private static int[] hexDecode = new int[] {
            128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,
            128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,
            128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,
            0,  1,  2,  3,  4,  5,  6,  7,  8,  9,128,128,128,128,128,128,
            128, 10, 11, 12, 13, 14, 15,128,128,128,128,128,128,128,128,128,
            128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,
            128, 10, 11, 12, 13, 14, 15,128,128,128,128,128,128,128,128,128,
            128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,
            128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,
            128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,
            128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,
            128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,
            128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,
            128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,
            128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,
            128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,128
        };

        public static string decodeString(string encoded)
        {
            StringBuilder decoded = new();
            for (int i = 0; i < encoded.Length; i += 2)
            {
                string hexStr = encoded.Substring(i, 2);
                int hexVal = Convert.ToInt32(hexStr, 16);
                if (hexVal == 0x25)
                { // '%'
                    string hex1 = encoded.Substring(i + 2, 2);
                    string hex2 = encoded.Substring(i + 4, 2);
                    int decodedVal = (hexDecode[Convert.ToInt32(hex1, 16)] << 4) | hexDecode[Convert.ToInt32(hex2, 16)];
                    decoded.Append(decodedVal.ToString("X"));
                    i += 4; // skip the next 4 characters
                }
                else
                {
                    decoded.Append(hexStr);
                }
            }
            return decoded.ToString();
        }
    }
}
