using System;
using System.IO;

namespace ZeroZip.Core
{
    /// <summary>
    /// Flags stored in the SFX footer describing how the payload is laid out.
    /// </summary>
    [Flags]
    public enum ZtarFlags : byte
    {
        None = 0,
        /// <summary>Payload bytes are appended directly to the end of the stub executable.</summary>
        Appended = 1 << 0,
        /// <summary>Payload bytes live in external multi-part volumes (base.001, base.002 ...).</summary>
        MultiPart = 1 << 1,
        /// <summary>Payload is AES-256 encrypted (a crypto header precedes the compressed data).</summary>
        Encrypted = 1 << 2,
        /// <summary>
        /// Payload was precompressed (e.g. via precomp) before codec compression. The decompressed
        /// payload is a container [precomp.exe][.pcf] that must be restored, not a raw TAR.
        /// </summary>
        Precompressed = 1 << 3,
    }



    /// <summary>
    /// Fixed-size trailer written at the very end of an ZeroZip SFX executable (or a
    /// stand-alone .szip container). The extractor locates it by reading the last
    /// <see cref="Size"/> bytes of the file and validating <see cref="Magic"/>.
    /// </summary>
    public sealed class ZtarFooter
    {
        /// <summary>"ZTARSFX" + version byte 0x01 packed as a little-endian ulong.</summary>
        public const ulong Magic = 0x01_58_46_53_52_41_54_5AUL; // 'Z','T','A','R','S','F','X',0x01
        public const byte CurrentVersion = 1;

        /// <summary>Total on-disk size of the serialized footer, in bytes.</summary>
        public const int Size = 48;

        /// <summary>Offset (within the SFX file) where the compressed payload begins. Appended mode only.</summary>
        public long PayloadOffset { get; set; }
        /// <summary>Length of the compressed payload in bytes (total across all parts).</summary>
        public long PayloadSize { get; set; }
        /// <summary>Sum of the uncompressed source bytes, for ratio reporting. -1 if unknown.</summary>
        public long OriginalSize { get; set; } = -1;
        /// <summary>CRC32 of the compressed payload bytes.</summary>
        public uint Crc32 { get; set; }
        public byte Version { get; set; } = CurrentVersion;
        public ZtarFlags Flags { get; set; }
        /// <summary>zstd windowLog used at compression time (informational).</summary>
        public byte WindowLog { get; set; }
        /// <summary>Codec applied to the payload.</summary>
        public CompressionMethod Method { get; set; } = CompressionMethod.Zstd;
        /// <summary>Number of external volumes when <see cref="ZtarFlags.MultiPart"/> is set; 0 otherwise.</summary>
        public int PartCount { get; set; }

        public bool IsMultiPart => (Flags & ZtarFlags.MultiPart) != 0;
        public bool IsAppended => (Flags & ZtarFlags.Appended) != 0;
        public bool IsEncrypted => (Flags & ZtarFlags.Encrypted) != 0;
        public bool IsPrecompressed => (Flags & ZtarFlags.Precompressed) != 0;

        public void Write(Stream stream)
        {
            using var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            bw.Write(PayloadOffset);   // 8   @0
            bw.Write(PayloadSize);     // 8   @8
            bw.Write(OriginalSize);    // 8   @16
            bw.Write(Crc32);           // 4   @24
            bw.Write(Version);         // 1   @28
            bw.Write((byte)Flags);     // 1   @29
            bw.Write(WindowLog);       // 1   @30
            bw.Write((byte)Method);    // 1   @31
            bw.Write(PartCount);       // 4   @32
            bw.Write(0);               // 4   @36 reserved (keeps magic at the tail)
            bw.Write(Magic);           // 8   @40 -> trailing 8 bytes of the file
            bw.Flush();
            // total = 48 bytes; magic occupies the final 8 bytes for tail detection.
        }

        /// <summary>
        /// Reads and validates a footer from the last <see cref="Size"/> bytes of the stream.
        /// Returns null when the trailing magic is absent (i.e. not an ZeroZip SFX file).
        /// </summary>
        public static ZtarFooter? ReadFromEnd(Stream seekableStream)
        {
            if (!seekableStream.CanSeek) throw new ArgumentException("Stream must be seekable.", nameof(seekableStream));
            if (seekableStream.Length < Size) return null;

            seekableStream.Seek(-Size, SeekOrigin.End);
            using var br = new BinaryReader(seekableStream, System.Text.Encoding.UTF8, leaveOpen: true);

            long payloadOffset = br.ReadInt64();
            long payloadSize = br.ReadInt64();
            long originalSize = br.ReadInt64();
            uint crc = br.ReadUInt32();
            byte version = br.ReadByte();
            byte flags = br.ReadByte();
            byte windowLog = br.ReadByte();
            byte method = br.ReadByte();
            int partCount = br.ReadInt32();
            br.ReadInt32();  // reserved
            ulong magic = br.ReadUInt64();

            if (magic != Magic) return null;

            return new ZtarFooter
            {
                PayloadOffset = payloadOffset,
                PayloadSize = payloadSize,
                OriginalSize = originalSize,
                Crc32 = crc,
                Version = version,
                Flags = (ZtarFlags)flags,
                WindowLog = windowLog,
                Method = (CompressionMethod)method,
                PartCount = partCount,
            };
        }
    }
}
