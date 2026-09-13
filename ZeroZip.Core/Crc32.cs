using System;

namespace ZeroZip.Core
{
    /// <summary>
    /// Standard CRC-32 (IEEE 802.3, polynomial 0xEDB88320) with an incremental API.
    /// Used to verify payload integrity on extraction.
    /// </summary>
    public sealed class Crc32
    {
        private static readonly uint[] Table = BuildTable();
        private uint _crc = 0xFFFFFFFFu;

        private static uint[] BuildTable()
        {
            const uint poly = 0xEDB88320u;
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? poly ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }

        public void Append(ReadOnlySpan<byte> data)
        {
            uint crc = _crc;
            foreach (byte b in data)
                crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            _crc = crc;
        }

        /// <summary>Final CRC value. Calling this does not reset internal state.</summary>
        public uint Value => _crc ^ 0xFFFFFFFFu;

        public void Reset() => _crc = 0xFFFFFFFFu;

        public static uint Compute(ReadOnlySpan<byte> data)
        {
            var c = new Crc32();
            c.Append(data);
            return c.Value;
        }
    }
}
