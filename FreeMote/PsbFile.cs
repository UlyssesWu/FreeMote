using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FreeMote
{
    public enum EncodeMode
    {
        Encrypt,
        Decrypt,
    }

    public enum EncodePosition
    {
        Body,
        Header,
        Full,
        /// <summary>
        /// Automata
        /// <para>if encrypt v3, will only encrypt header as GoGoNippon format.</para>
        /// <para>if encrypt v2, will only encrypt body like most games.</para>
        /// <para>if decrypt, clean header and body both.</para>
        /// </summary>
        Auto
    }

    public class PsbFile
    {
        public string Path { get; private set; }
        internal PsbHeader Header { get; set; }

        public ushort Version => Header.Version;

        public bool IsHeaderEncrypted => Header.HeaderEncrypt != 0;

        public bool IsBodyEncrypted => Header.OffsetEncrypt != Header.OffsetNames;

        public PsbFile(string path)
        {
            Path = path;
            ParseHeader();
        }

        /// <summary>
        /// Parse PSB Header
        /// </summary>
        public void ParseHeader()
        {
            if (!File.Exists(Path))
            {
                throw new FileNotFoundException("Can not load file.", Path);
            }
            using (var fs = File.OpenRead(Path))
            {
                BinaryReader br = new BinaryReader(fs);
                Header = PsbHeader.Load(br);
            }
        }

        /// <summary>
        /// Parse PSB encrypted header with key
        /// </summary>
        public void ParseHeader(uint key)
        {
            if (!File.Exists(Path))
            {
                throw new FileNotFoundException("Can not load file.", Path);
            }
            using (var fs = File.OpenRead(Path))
            {
                BinaryReader br = new BinaryReader(fs);
                Header = PsbHeader.Load(br, key);
            }
        }

        /// <summary>
        /// Try to test if header is encrypted actually, based on assumption. Only work for E-mote PSB
        /// </summary>
        /// <returns>true if header seems to be encrypted</returns>
        public bool TestHeaderEncrypted()
        {
            FileInfo fi = new FileInfo(Path);

            if (Header.OffsetEncrypt < fi.Length
                && Header.OffsetNames != 0
                && (Header.OffsetEncrypt == Header.OffsetNames || Header.OffsetEncrypt == 0))
            {
                return false;
            }
            return true;
        }

        private static bool TestHeaderEncrypted(BinaryReader br, PsbHeader header)
        {
            if (header.OffsetEncrypt < br.BaseStream.Length
                && header.OffsetNames != 0
                && (header.OffsetEncrypt == header.OffsetNames || header.OffsetEncrypt == 0))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Try to test if body is encrypted actually, based on assumption. Only work for E-mote PSB
        /// </summary>
        /// <returns>true if body seems to be encrypted</returns>
        public bool TestBodyEncrypted()
        {
            using (var fs = File.OpenRead(Path))
            {
                BinaryReader br = new BinaryReader(fs);
                if (!TestBodyEncrypted(br, Header)) return false;
            }
            return true;
        }

        private static bool TestBodyEncrypted(BinaryReader br, PsbHeader header)
        {
            long pos = br.BaseStream.Position;

            switch (header.Version)
            {
                case 1:
                case 2:
                    br.BaseStream.Seek(40, SeekOrigin.Begin);
                    break;
                case 3:
                    br.BaseStream.Seek(44, SeekOrigin.Begin);
                    break;
                default:
                    br.BaseStream.Seek(56, SeekOrigin.Begin);
                    break;
            }
            if (br.ReadByte() == 0x0E)
            {
                br.ReadBytes(2);
                if (br.ReadByte() == 0x0E)
                {
                    br.BaseStream.Seek(pos, SeekOrigin.Begin);
                    return false;
                }
            }
            br.BaseStream.Seek(pos, SeekOrigin.Begin);
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="br"></param>
        /// <param name="bw"></param>
        /// <param name="cleanEncryptOffset">maybe you should not use it</param>
        /// <param name="resetEncryptOffset">restore offsetEncrypt</param>
        /// <returns>true if seems encrypted</returns>
        private static bool WriteOriginalPartialHeader(BinaryReader br, BinaryWriter bw, PsbHeader header, bool cleanEncryptOffset = false, bool resetEncryptOffset = false)
        {
            uint offsetEncrypt = br.ReadUInt32();
            header.OffsetEncrypt = cleanEncryptOffset ? 0 : offsetEncrypt;
            header.OffsetNames = br.ReadUInt32();
            if (resetEncryptOffset)
            {
                header.OffsetEncrypt = header.OffsetNames;
            }
            header.OffsetStrings = br.ReadUInt32();
            header.OffsetStringsData = br.ReadUInt32();
            header.OffsetChunkOffsets = br.ReadUInt32();
            header.OffsetChunkLengths = br.ReadUInt32();
            header.OffsetChunkData = br.ReadUInt32();
            header.EntryCounts = br.ReadUInt32();
            if (header.Version > 2)
            {
                header.Checksum = br.ReadUInt32();
            }
            if (header.Version > 3)
            {
                header.OffsetUnknown1 = br.ReadUInt32();
                header.OffsetUnknown2 = br.ReadUInt32();
                header.OffsetUnknown3 = br.ReadUInt32();
            }
            bw.Write(header.OffsetEncrypt);
            bw.Write(header.OffsetNames);
            bw.Write(header.OffsetStrings);
            bw.Write(header.OffsetStringsData);
            bw.Write(header.OffsetChunkOffsets);
            bw.Write(header.OffsetChunkLengths);
            bw.Write(header.OffsetChunkData);
            bw.Write(header.EntryCounts);
            if (header.Version > 2)
            {
                bw.Write(header.Checksum);
            }
            if (header.Version > 3)
            {
                bw.Write(header.OffsetUnknown1);
                bw.Write(header.OffsetUnknown2);
                bw.Write(header.OffsetUnknown3);
            }
            return (offsetEncrypt != 0x2C) && (offsetEncrypt != 0x38) && (offsetEncrypt != 0);
        }

        /// <summary>
        /// Assume header is encrypted
        /// </summary>
        /// <param name="br"></param>
        /// <param name="bw"></param>
        /// <param name="context"></param>
        private static void WriteDecryptPartialHeader(BinaryReader br, BinaryWriter bw, PsbStreamContext context, PsbHeader header)
        {
            header.OffsetEncrypt = context.ReadUInt32(br);
            header.OffsetNames = context.ReadUInt32(br);
            header.OffsetStrings = context.ReadUInt32(br);
            header.OffsetStringsData = context.ReadUInt32(br);
            header.OffsetChunkOffsets = context.ReadUInt32(br);
            header.OffsetChunkLengths = context.ReadUInt32(br);
            header.OffsetChunkData = context.ReadUInt32(br);
            header.EntryCounts = context.ReadUInt32(br);
            if (header.Version > 2)
            {
                header.Checksum = context.ReadUInt32(br);
            }
            if (header.Version > 3) //is M2 intend to cheat us by wrong OffsetEncrypt?
            {
                header.OffsetUnknown1 = context.ReadUInt32(br);
                header.OffsetUnknown2 = context.ReadUInt32(br);
                header.OffsetUnknown3 = context.ReadUInt32(br);
            }

            //var checksumStartPosition = bw.BaseStream.Position;
            bw.Write(header.OffsetEncrypt);
            bw.Write(header.OffsetNames);
            bw.Write(header.OffsetStrings);
            bw.Write(header.OffsetStringsData);
            bw.Write(header.OffsetChunkOffsets);
            bw.Write(header.OffsetChunkLengths);
            bw.Write(header.OffsetChunkData);
            bw.Write(header.EntryCounts);
            //var checksumPosition = bw.BaseStream.Position;

            if (header.Version > 2)
            {
                int checkLength = 32;
                bw.BaseStream.Seek(-checkLength, SeekOrigin.Current);
                var checkBuffer = new byte[checkLength];
                bw.BaseStream.Read(checkBuffer, 0, checkLength);
                Adler32 adler32 = new Adler32();
                adler32.Update(checkBuffer);
                header.Checksum = (uint)adler32.Checksum;
                if (header.Version == 3)
                {
                    bw.Write(header.Checksum);
                }
                else //PSBv4
                {
                    checkBuffer = BitConverter.GetBytes(header.OffsetUnknown1)
                        .Concat(BitConverter.GetBytes(header.OffsetUnknown2))
                        .Concat(BitConverter.GetBytes(header.OffsetUnknown3)).ToArray();
                    adler32.Update(checkBuffer);
                    header.Checksum = (uint)adler32.Checksum;
                    bw.Write(header.Checksum);
                    bw.Write(header.OffsetUnknown1);
                    bw.Write(header.OffsetUnknown2);
                    bw.Write(header.OffsetUnknown3);
                }
            }
        }

        /// <summary>
        /// Assume header is clean
        /// </summary>
        /// <param name="br"></param>
        /// <param name="bw"></param>
        /// <param name="context"></param>
        private static void WriteEncryptPartialHeader(BinaryReader br, BinaryWriter bw, PsbStreamContext context, PsbHeader header)
        {
            var checksumStartPosition = br.BaseStream.Position;
            header.OffsetEncrypt = br.ReadUInt32();
            header.OffsetNames = br.ReadUInt32();
            if (header.OffsetEncrypt == 0)
            {
                header.OffsetEncrypt = header.OffsetNames;
            }
            header.OffsetStrings = br.ReadUInt32();
            header.OffsetStringsData = br.ReadUInt32();
            header.OffsetChunkOffsets = br.ReadUInt32();
            header.OffsetChunkLengths = br.ReadUInt32();
            header.OffsetChunkData = br.ReadUInt32();
            header.EntryCounts = br.ReadUInt32();
            if (header.Version > 2)
            {
                header.Checksum = br.ReadUInt32();
            }
            if (header.Version > 3)
            {
                header.OffsetUnknown1 = br.ReadUInt32();
                header.OffsetUnknown2 = br.ReadUInt32();
                header.OffsetUnknown3 = br.ReadUInt32();
            }
            var checksumEndPosition = br.BaseStream.Position;

            context.Write(header.OffsetEncrypt, bw);
            context.Write(header.OffsetNames, bw);
            context.Write(header.OffsetStrings, bw);
            context.Write(header.OffsetStringsData, bw);
            context.Write(header.OffsetChunkOffsets, bw);
            context.Write(header.OffsetChunkLengths, bw);
            context.Write(header.OffsetChunkData, bw);
            context.Write(header.EntryCounts, bw);

            if (header.Version > 2)
            {
                int checkLength = 32;
                br.BaseStream.Seek(checksumStartPosition, SeekOrigin.Begin);
                var checkBuffer = new byte[checkLength];
                br.BaseStream.Read(checkBuffer, 0, checkLength);
                br.BaseStream.Seek(checksumEndPosition, SeekOrigin.Begin); //Jump back
                Adler32 adler32 = new Adler32();
                adler32.Update(checkBuffer);
                header.Checksum = (uint)adler32.Checksum;
                if (header.Version == 3)
                {
                    context.Write(header.Checksum, bw);
                }
                else //PSBv4
                {
                    checkBuffer = BitConverter.GetBytes(header.OffsetUnknown1)
                                            .Concat(BitConverter.GetBytes(header.OffsetUnknown2))
                                            .Concat(BitConverter.GetBytes(header.OffsetUnknown3)).ToArray();
                    adler32.Update(checkBuffer);
                    header.Checksum = (uint)adler32.Checksum;
                    context.Write(header.Checksum, bw);
                    context.Write(header.OffsetUnknown1, bw);
                    context.Write(header.OffsetUnknown2, bw);
                    context.Write(header.OffsetUnknown3, bw);
                }
            }
        }

        private static void WriteOriginalBody(BinaryReader br, BinaryWriter bw)
        {
            WriteToEnd(br, bw);
        }

        private static void WriteEncodeBody(BinaryReader br, BinaryWriter bw, PsbStreamContext context, PsbHeader header)
        {
            bw.Write
                (
                    context.Encode
                    (
                        br.ReadBytes((int)(header.OffsetChunkOffsets - header.OffsetNames))
                    )
                );
            WriteToEnd(br, bw);
        }

        private static void WriteToEnd(BinaryReader br, BinaryWriter bw)
        {
            bw.Write
            (
                br.ReadBytes((int)(br.BaseStream.Length - br.BaseStream.Position))
            );
        }

        public void EncodeToFile(uint key, string savePath, EncodeMode mode = EncodeMode.Encrypt, EncodePosition position = EncodePosition.Auto)
        {
            using (var input = File.OpenRead(Path))
            {
                using (var output = File.Open(savePath, FileMode.Create, FileAccess.ReadWrite))
                {
                    var header = Encode(key, mode, position, input, output);
                    if (header != null)
                    {
                        Header = header;
                    }
                }
            }
        }

        /// <summary>
        /// Transfer from an old key to a new key
        /// </summary>
        /// <param name="oldKey"></param>
        /// <param name="newKey"></param>
        /// <param name="inputBytes"></param>
        /// <returns></returns>
        public static byte[] Transfer(uint oldKey, uint newKey, byte[] inputBytes)
        {
            var input = new MemoryStream(inputBytes);
            var output = new MemoryStream((int)input.Length);
            Encode(oldKey, EncodeMode.Decrypt, EncodePosition.Auto, input, output);
            Encode(newKey, EncodeMode.Encrypt, EncodePosition.Auto, output, input);
            inputBytes = input.ToArray();
            input.Dispose();
            output.Dispose();
            return inputBytes;
        }

        /// <summary>
        /// Encode a psb byte array manually
        /// </summary>
        /// <param name="key"></param>
        /// <param name="inputBytes"></param>
        /// <param name="mode"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public static byte[] EncodeToBytes(uint key, byte[] inputBytes, EncodeMode mode = EncodeMode.Encrypt, EncodePosition position = EncodePosition.Auto)
        {
            using (var input = new MemoryStream(inputBytes))
            {
                using (var output = new MemoryStream((int)input.Length))
                {
                    Encode(key, mode, position, input, output);
                    return output.ToArray();
                }
            }
        }
        private static PsbHeader Encode(uint key, EncodeMode mode, EncodePosition position, Stream input, Stream output)
        {
            input.Seek(0, SeekOrigin.Begin);
            output.Seek(0, SeekOrigin.Begin);
            PsbHeader header = new PsbHeader();
            PsbStreamContext context = new PsbStreamContext(key);
            BinaryReader br = new BinaryReader(input);
            BinaryWriter bw = new BinaryWriter(output);
            header.Signature = br.ReadChars(4);
            header.Version = br.ReadUInt16();
            bw.Write(header.Signature); //Signature
            bw.Write(header.Version); //Version
            header.HeaderEncrypt = br.ReadUInt16(); //headerEncrypt, sometimes we don't believe it when encoding
            header.OffsetEncrypt = br.ReadUInt32();
            header.OffsetNames = br.ReadUInt32();
            br.BaseStream.Seek(-8, SeekOrigin.Current);

            void WriteOriginal()
            {
                bw.Write(header.HeaderEncrypt);
                WriteOriginalPartialHeader(br, bw, header);
                WriteOriginalBody(br, bw);
            }

            switch (position)
            {
                case EncodePosition.Auto:
                    bool headerEnc = TestHeaderEncrypted(br, header);
                    bool bodyEnc = TestBodyEncrypted(br, header);
                    if (headerEnc && bodyEnc) //it's already encrypted.
                    {
                        mode = EncodeMode.Decrypt;
                    }
                    if (!headerEnc && !bodyEnc)
                    {
                        mode = EncodeMode.Encrypt;
                    }
                    switch (mode)
                    {
                        case EncodeMode.Encrypt:
                            if (header.Version > 2) //Header Encrypted; Body Clean
                            {
                                bw.Write((ushort)1);
                                if (headerEnc)
                                {
                                    WriteOriginalPartialHeader(br, bw, header);
                                    WriteOriginalBody(br, bw);
                                    break;
                                }
                                WriteEncryptPartialHeader(br, bw, context, header);
                                WriteOriginalBody(br, bw);
                                break;
                            }
                            else //Header Clean; Body Encrpyted
                            {
                                bw.Write((ushort)0); //
                                if (headerEnc)
                                {
                                    WriteDecryptPartialHeader(br, bw, context, header);
                                    context = new PsbStreamContext(key);
                                    WriteEncodeBody(br, bw, context, header);
                                }
                                else
                                {
                                    WriteOriginalPartialHeader(br, bw, header, resetEncryptOffset: true);
                                    WriteEncodeBody(br, bw, context, header);
                                }
                            }
                            break;
                        case EncodeMode.Decrypt:
                            bw.Write((ushort)0); //
                            if (headerEnc)
                            {
                                WriteDecryptPartialHeader(br, bw, context, header);
                            }
                            else
                            {
                                WriteOriginalPartialHeader(br, bw, header);
                            }
                            if (bodyEnc)
                            {
                                WriteEncodeBody(br, bw, context, header);
                            }
                            else
                            {
                                WriteOriginalBody(br, bw);
                            }
                            break;
                        default:
                            WriteOriginal();
                            break;
                    }
                    break;
                case EncodePosition.Body:
                    switch (mode)
                    {
                        case EncodeMode.Encrypt:
                        case EncodeMode.Decrypt:
                            bw.Write(header.HeaderEncrypt);
                            //We believe file is clean so write original header but encrypt body
                            WriteOriginalPartialHeader(br, bw, header);
                            WriteEncodeBody(br, bw, context, header);
                            break;
                        default:
                            WriteOriginal();
                            break;
                    }
                    break;
                case EncodePosition.Header:
                    switch (mode)
                    {
                        case EncodeMode.Encrypt:
                            bw.Write((ushort)1);
                            WriteEncryptPartialHeader(br, bw, context, header);
                            WriteOriginalBody(br, bw);
                            break;
                        case EncodeMode.Decrypt:
                            bw.Write((ushort)0); //
                            WriteDecryptPartialHeader(br, bw, context, header);
                            WriteOriginalBody(br, bw);
                            break;
                        default:
                            WriteOriginal();
                            break;
                    }
                    break;
                case EncodePosition.Full:
                    switch (mode)
                    {
                        case EncodeMode.Encrypt:
                            bw.Write((ushort)1);
                            WriteEncryptPartialHeader(br, bw, context, header);
                            WriteEncodeBody(br, bw, context, header);
                            break;
                        case EncodeMode.Decrypt:
                            bw.Write((ushort)1); //
                            WriteDecryptPartialHeader(br, bw, context, header);
                            WriteEncodeBody(br, bw, context, header);
                            break;
                        default:
                            WriteOriginal();
                            break;
                    }
                    break;
                default:
                    WriteOriginal();
                    break;
            }
            bw.Flush();
            return header;
        }

    }
}

