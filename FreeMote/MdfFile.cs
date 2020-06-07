using System;
using System.IO;
using System.Text;

namespace FreeMote
{
    /// <summary>
    /// MDF
    /// </summary> 
    /// Merged Data File
    /// Compressed PSB
    public static class MdfFile
    {
        /// <summary>
        /// MDF Signature
        /// </summary>
        public const string Signature = "mdf";

        public static bool IsSignatureMdf(Stream stream)
        {
            var header = new byte[4];
            var pos = stream.Position;
            stream.Read(header, 0, 4);
            stream.Position = pos;
            if (header[0] == 'm' && header[1] == 'd' && header[2] == 'f' && header[3] == 0)
            {
                return true;
            }

            return false;
        }

        public static int MdfGetOriginalLength(this Stream stream)
        {
            var pos = stream.Position;
            stream.Seek(4, SeekOrigin.Begin);
            var buffer = new byte[4];
            stream.Read(buffer, 0, 4);
            stream.Seek(pos, SeekOrigin.Begin);
            return BitConverter.ToInt32(buffer, 0);
        }

        public static void DecompressToPsbFile(string inputPath, string outputPath)
        {
            Stream mfs = File.OpenRead(inputPath);
            mfs.Seek(10, SeekOrigin.Begin);
            File.WriteAllBytes(outputPath, ZlibCompress.Decompress(mfs));
            mfs.Dispose();
        }

        public static Stream DecompressToPsbStream(Stream input, int size = 0)
        {
            input.Seek(10, SeekOrigin.Begin);
            return ZlibCompress.DecompressToStream(input, size);
        }

        public static Stream CompressPsbToMdfStream(Stream input, bool fast = true)
        {
            var pos = input.Position;
            Adler32 checksumer = new Adler32();
            checksumer.Update(input);
            var checksum = (uint) checksumer.Checksum;
            checksumer = null;
            input.Position = pos;
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8, true);
            bw.WriteStringZeroTrim(Signature);
            bw.Write((uint) input.Length);
            bw.Write(ZlibCompress.Compress(input, fast));
            bw.WriteBE(checksum);
            bw.Flush();
            bw.Dispose();
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public static void CompressToMdfFile(this PsbFile psbFile, string outputPath = null, bool fast = true)
        {
            var bytes = File.ReadAllBytes(psbFile.Path);
            Adler32 checksumer = new Adler32();
            checksumer.Update(bytes);
            var checksum = (uint) checksumer.Checksum;
            checksumer = null;
            MemoryStream ms = new MemoryStream(bytes);
            using (FileStream fs = new FileStream(outputPath ?? psbFile.Path + ".mdf", FileMode.Create))
            {
                BinaryWriter bw = new BinaryWriter(fs);
                bw.WriteStringZeroTrim(Signature);
                bw.Write((uint) ms.Length);
                bw.Write(ZlibCompress.Compress(ms, fast));
                bw.WriteBE(checksum);
                ms.Dispose();
                bw.Flush();
            }
        }
    }
}