using System;
using System.IO;
using System.Linq;
using System.Text;

namespace FreeMote
{
    public class PsbFile
    {
        public string Path { get; private set; }
        public PsbHeader Header { get; set; }

        public ushort Version => Header.Version;
        public bool IsMdf { get; private set; } = false;

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
                ParseHeader(fs);
            }
        }

        private void ParseHeader(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream, Encoding.UTF8, true);
            var sig = new string(br.ReadChars(4)).ToUpperInvariant();
            if (sig.StartsWith("MDF"))
            {
                IsMdf = true;
                Header = new PsbHeader();
                return;
            }

            br.BaseStream.Seek(0, SeekOrigin.Begin);
            Header = PsbHeader.Load(br);
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

            if (Header.HeaderLength < fi.Length
                && Header.OffsetNames != 0
                && (Header.HeaderLength == Header.OffsetNames || Header.HeaderLength == 0))
            {
                return false;
            }

            return true;
        }

        [Obsolete("Not Implemented")]
        private static bool TestKeyValidForHeader()
        {
            throw new NotImplementedException();
        }

        [Obsolete("Not Implemented")]
        private static bool TestKeyValidForBody()
        {
            throw new NotImplementedException();
        }

        public static bool TestHeaderEncrypted(Stream stream, PsbHeader header)
        {
            //MARK: Not always works
            if (header.HeaderLength < stream.Length
                && header.OffsetNames != 0
                && (header.HeaderLength == header.OffsetNames || header.HeaderLength == 0))
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

        public static bool TestBodyEncrypted(BinaryReader br, PsbHeader header)
        {
            //MARK: Not always works
            var pos = br.BaseStream.Position;

            switch (header.Version)
            {
                case 2:
                case 3:
                case 4:
                    br.BaseStream.Seek(header.GetHeaderLength(), SeekOrigin.Begin);
                    break;
                default:
                    br.BaseStream.Seek(header.OffsetNames, SeekOrigin.Begin);
                    break;
            }

            var arrayType = br.ReadByte();
            const byte arrayN0 = 0x0C;
            const byte arrayMaxSize = 4;
            const byte entryMaxSize = 4;

            if (arrayType > arrayN0 && arrayType <= arrayN0 + arrayMaxSize) //ArrayN4 >= Type >= ArrayN1
            {
                br.ReadBytes(arrayType - arrayN0); //Array Length
                var entryLength = br.ReadByte(); //Entry Length
                if (entryLength > arrayN0 && entryLength <= arrayN0 + entryMaxSize) //EntryLength == 1 || 2
                {
                    br.BaseStream.Seek(pos, SeekOrigin.Begin);
                    return false;
                }
            }

            br.BaseStream.Seek(pos, SeekOrigin.Begin);
            return true;
        }

        private static void WriteOriginalPartialHeader(BinaryReader br, BinaryWriter bw, PsbHeader header)
        {
            header.HeaderLength = br.ReadUInt32();
            header.OffsetNames = br.ReadUInt32();
            header.OffsetStrings = br.ReadUInt32();
            header.OffsetStringsData = br.ReadUInt32();
            header.OffsetChunkOffsets = br.ReadUInt32();
            header.OffsetChunkLengths = br.ReadUInt32();
            header.OffsetChunkData = br.ReadUInt32();
            header.OffsetEntries = br.ReadUInt32();
            if (header.Version > 2)
            {
                header.Checksum = br.ReadUInt32();
            }

            if (header.Version > 3)
            {
                header.OffsetExtraChunkOffsets = br.ReadUInt32();
                header.OffsetExtraChunkLengths = br.ReadUInt32();
                header.OffsetExtraChunkData = br.ReadUInt32();
            }

            bw.Write(header.HeaderLength);
            bw.Write(header.OffsetNames);
            bw.Write(header.OffsetStrings);
            bw.Write(header.OffsetStringsData);
            bw.Write(header.OffsetChunkOffsets);
            bw.Write(header.OffsetChunkLengths);
            bw.Write(header.OffsetChunkData);
            bw.Write(header.OffsetEntries);
            if (header.Version > 2)
            {
                bw.Write(header.Checksum);
            }

            if (header.Version > 3)
            {
                bw.Write(header.OffsetExtraChunkOffsets);
                bw.Write(header.OffsetExtraChunkLengths);
                bw.Write(header.OffsetExtraChunkData);
            }
        }

        /// <summary>
        /// Assume header is encrypted
        /// </summary>
        /// <param name="br"></param>
        /// <param name="bw"></param>
        /// <param name="context"></param>
        /// <param name="header"></param>
        private static void WriteDecryptPartialHeader(BinaryReader br, BinaryWriter bw, PsbStreamContext context,
            PsbHeader header)
        {
            header.HeaderLength = context.ReadUInt32(br);
            header.OffsetNames = context.ReadUInt32(br);
            header.OffsetStrings = context.ReadUInt32(br);
            header.OffsetStringsData = context.ReadUInt32(br);
            header.OffsetChunkOffsets = context.ReadUInt32(br);
            header.OffsetChunkLengths = context.ReadUInt32(br);
            header.OffsetChunkData = context.ReadUInt32(br);
            header.OffsetEntries = context.ReadUInt32(br);
            if (header.Version > 2)
            {
                header.Checksum = context.ReadUInt32(br);
            }

            if (header.Version > 3)
            {
                header.OffsetExtraChunkOffsets = context.ReadUInt32(br);
                header.OffsetExtraChunkLengths = context.ReadUInt32(br);
                header.OffsetExtraChunkData = context.ReadUInt32(br);
            }

            //var checksumStartPosition = bw.BaseStream.Position;
            bw.Write(header.HeaderLength);
            bw.Write(header.OffsetNames);
            bw.Write(header.OffsetStrings);
            bw.Write(header.OffsetStringsData);
            bw.Write(header.OffsetChunkOffsets);
            bw.Write(header.OffsetChunkLengths);
            bw.Write(header.OffsetChunkData);
            bw.Write(header.OffsetEntries);
            //var checksumPosition = bw.BaseStream.Position;

            if (header.Version > 2)
            {
                int checkLength = 32;
                bw.BaseStream.Seek(-checkLength, SeekOrigin.Current);
                var checkBuffer = new byte[checkLength];
                bw.BaseStream.Read(checkBuffer, 0, checkLength);
                Adler32 adler32 = new Adler32();
                adler32.Update(checkBuffer);
                header.Checksum = (uint) adler32.Checksum;
                if (header.Version == 3)
                {
                    bw.Write(header.Checksum);
                }
                else //PSBv4
                {
                    checkBuffer = BitConverter.GetBytes(header.OffsetExtraChunkOffsets)
                        .Concat(BitConverter.GetBytes(header.OffsetExtraChunkLengths))
                        .Concat(BitConverter.GetBytes(header.OffsetExtraChunkData)).ToArray();
                    adler32.Update(checkBuffer);
                    header.Checksum = (uint) adler32.Checksum;
                    bw.Write(header.Checksum);
                    bw.Write(header.OffsetExtraChunkOffsets);
                    bw.Write(header.OffsetExtraChunkLengths);
                    bw.Write(header.OffsetExtraChunkData);
                }
            }
        }

        /// <summary>
        /// Assume header is clean
        /// </summary>
        /// <param name="br"></param>
        /// <param name="bw"></param>
        /// <param name="context"></param>
        /// <param name="header"></param>
        private static void WriteEncryptPartialHeader(BinaryReader br, BinaryWriter bw, PsbStreamContext context,
            PsbHeader header)
        {
            var checksumStartPosition = br.BaseStream.Position;
            header.HeaderLength = br.ReadUInt32();
            header.OffsetNames = br.ReadUInt32();
            if (header.HeaderLength == 0)
            {
                header.HeaderLength = header.OffsetNames;
            }

            header.OffsetStrings = br.ReadUInt32();
            header.OffsetStringsData = br.ReadUInt32();
            header.OffsetChunkOffsets = br.ReadUInt32();
            header.OffsetChunkLengths = br.ReadUInt32();
            header.OffsetChunkData = br.ReadUInt32();
            header.OffsetEntries = br.ReadUInt32();
            if (header.Version > 2)
            {
                header.Checksum = br.ReadUInt32();
            }

            if (header.Version > 3)
            {
                header.OffsetExtraChunkOffsets = br.ReadUInt32();
                header.OffsetExtraChunkLengths = br.ReadUInt32();
                header.OffsetExtraChunkData = br.ReadUInt32();
            }

            var checksumEndPosition = br.BaseStream.Position;

            context.Write(header.HeaderLength, bw);
            context.Write(header.OffsetNames, bw);
            context.Write(header.OffsetStrings, bw);
            context.Write(header.OffsetStringsData, bw);
            context.Write(header.OffsetChunkOffsets, bw);
            context.Write(header.OffsetChunkLengths, bw);
            context.Write(header.OffsetChunkData, bw);
            context.Write(header.OffsetEntries, bw);

            if (header.Version > 2)
            {
                int checkLength = 32;
                br.BaseStream.Seek(checksumStartPosition, SeekOrigin.Begin);
                var checkBuffer = new byte[checkLength];
                br.BaseStream.Read(checkBuffer, 0, checkLength);
                br.BaseStream.Seek(checksumEndPosition, SeekOrigin.Begin); //Jump back
                Adler32 adler32 = new Adler32();
                adler32.Update(checkBuffer);
                header.Checksum = (uint) adler32.Checksum;
                if (header.Version == 3)
                {
                    context.Write(header.Checksum, bw);
                }
                else //PSBv4
                {
                    checkBuffer = BitConverter.GetBytes(header.OffsetExtraChunkOffsets)
                        .Concat(BitConverter.GetBytes(header.OffsetExtraChunkLengths))
                        .Concat(BitConverter.GetBytes(header.OffsetExtraChunkData)).ToArray();
                    adler32.Update(checkBuffer);
                    header.Checksum = (uint) adler32.Checksum;
                    context.Write(header.Checksum, bw);
                    context.Write(header.OffsetExtraChunkOffsets, bw);
                    context.Write(header.OffsetExtraChunkLengths, bw);
                    context.Write(header.OffsetExtraChunkData, bw);
                }
            }
        }

        private static void WriteOriginalBody(BinaryReader br, BinaryWriter bw)
        {
            WriteToEnd(br, bw);
        }

        private static void WriteEncodeBody(BinaryReader br, BinaryWriter bw, PsbStreamContext context,
            PsbHeader header)
        {
            bw.Write
            (
                context.Encode
                (
                    br.ReadBytes((int) (header.OffsetChunkOffsets - header.OffsetNames))
                )
            );
            WriteToEnd(br, bw);
        }

        private static void WriteToEnd(BinaryReader br, BinaryWriter bw)
        {
            bw.Write
            (
                br.ReadBytes((int) (br.BaseStream.Length - br.BaseStream.Position))
            );
        }

        /// <summary>
        /// Encrypt or decrypt PSB and write to a file
        /// </summary>
        /// <param name="key"></param>
        /// <param name="savePath"></param>
        /// <param name="mode"></param>
        /// <param name="position"></param>
        public void EncodeToFile(uint key, string savePath, EncodeMode mode = EncodeMode.Encrypt,
            EncodePosition position = EncodePosition.Auto)
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
            var output = new MemoryStream((int) input.Length);
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
        public static byte[] EncodeToBytes(uint key, byte[] inputBytes, EncodeMode mode = EncodeMode.Encrypt,
            EncodePosition position = EncodePosition.Auto)
        {
            using (var input = new MemoryStream(inputBytes))
            {
                using (var output = new MemoryStream((int) input.Length))
                {
                    Encode(key, mode, position, input, output);
                    return output.ToArray();
                }
            }
        }

        /// <summary>
        /// Encode (Encrypt/Decrypt) PSB file
        /// </summary>
        /// <param name="key"></param>
        /// <param name="mode"></param>
        /// <param name="position"></param>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <returns>Unencrypted Header for reference. Usually you shouldn't use it.</returns>
        public static PsbHeader Encode(uint key, EncodeMode mode, EncodePosition position, Stream input, Stream output)
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
            header.HeaderLength = br.ReadUInt32();
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
                    bool headerEnc = TestHeaderEncrypted(br.BaseStream, header);
                    bool bodyEnc = TestBodyEncrypted(br, header);
                    if (headerEnc && bodyEnc) //MARK: is this possible?
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
                                bw.Write((ushort) 1);
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
                                bw.Write((ushort) 0); //
                                if (headerEnc)
                                {
                                    WriteDecryptPartialHeader(br, bw, context, header);
                                    context = new PsbStreamContext(key);
                                    WriteEncodeBody(br, bw, context, header);
                                }
                                else
                                {
                                    WriteOriginalPartialHeader(br, bw, header);
                                    WriteEncodeBody(br, bw, context, header);
                                }
                            }

                            break;
                        case EncodeMode.Decrypt:
                            bw.Write((ushort) 0); //
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
                            bw.Write((ushort) 1);
                            WriteEncryptPartialHeader(br, bw, context, header);
                            WriteOriginalBody(br, bw);
                            break;
                        case EncodeMode.Decrypt:
                            bw.Write((ushort) 0); //
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
                            bw.Write((ushort) 1);
                            WriteEncryptPartialHeader(br, bw, context, header);
                            WriteEncodeBody(br, bw, context, header);
                            break;
                        case EncodeMode.Decrypt:
                            bw.Write((ushort) 1); //
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
            output.Seek(0, SeekOrigin.Begin);
            return header;
        }

        /// <summary>
        /// Test if the first 4 bytes belong to a correct PSB header
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static bool CheckSignature(Stream stream)
        {
            var header = new byte[4];
            var pos = stream.Position;
            stream.Read(header, 0, 4);
            stream.Position = pos;
            if (header[0] == 'P' && header[1] == 'S' && header[2] == 'B' && header[3] == 0)
            {
                return true;
            }

            return false;
        }
    }
}