using CastleLibrary.Utils.Conversion;
using HomeTools.Crypto;
using System.Numerics;

namespace HomeTools.UnBAR
{
    internal class EDATData
    {
        public long flags;
        public long blockSize;
        public BigInteger fileLen;

        public static EDATData createEDATData(byte[] data) => new()
        {
            flags = ConversionUtils.be32(data, 0),
            blockSize = ConversionUtils.be32(data, 4),
            fileLen = ConversionUtils.be64(data, 8)
        };

        public long getBlockSize() => blockSize;

        public BigInteger getFileLen() => fileLen;

        public long getFlags() => flags;
    }
}