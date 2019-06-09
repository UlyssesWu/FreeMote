using System.IO;

namespace FreeMote.Psb
{
    public static class PsbFileExtension
    {
        /// <summary>
        /// Save PSB as MDF file
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="path"></param>
        /// <param name="key"></param>
        public static void SaveAsMdfFile(this PSB psb, string path, uint? key = null)
        {
            psb.Merge();
            var bytes = psb.Build();
            Adler32 adler = new Adler32();
            uint checksum = 0;
            if (key == null)
            {
                adler.Update(bytes);
                checksum = (uint)adler.Checksum;
            }
            MemoryStream ms = new MemoryStream(bytes);
            using (Stream fs = new FileStream(path, FileMode.Create))
            {
                if (key != null)
                {
                    MemoryStream nms = new MemoryStream((int)ms.Length);
                    PsbFile.Encode(key.Value, EncodeMode.Encrypt, EncodePosition.Auto, ms, nms);
                    ms.Dispose();
                    ms = nms;
                    var pos = ms.Position;
                    adler.Update(ms);
                    checksum = (uint)adler.Checksum;
                    ms.Position = pos;
                }

                BinaryWriter bw = new BinaryWriter(fs);
                bw.WriteStringZeroTrim(MdfFile.Signature);
                bw.Write((uint)ms.Length);
                bw.Write(ZlibCompress.Compress(ms));
                bw.WriteBE(checksum);
                ms.Dispose();
                bw.Flush();
            }
        }

        public static byte[] SaveAsMdf(this PSB psb, uint? key = null)
        {
            psb.Merge();
            var bytes = psb.Build();
            Adler32 adler = new Adler32();
            uint checksum = 0;
            if (key == null)
            {
                adler.Update(bytes);
                checksum = (uint)adler.Checksum;
            }
            MemoryStream ms = new MemoryStream(bytes);
            using (MemoryStream fs = new MemoryStream())
            {
                if (key != null)
                {
                    MemoryStream nms = new MemoryStream((int)ms.Length);
                    PsbFile.Encode(key.Value, EncodeMode.Encrypt, EncodePosition.Auto, ms, nms);
                    ms.Dispose();
                    ms = nms;
                    var pos = ms.Position;
                    adler.Update(ms);
                    checksum = (uint)adler.Checksum;
                    ms.Position = pos;
                }

                BinaryWriter bw = new BinaryWriter(fs);
                bw.WriteStringZeroTrim(MdfFile.Signature);
                bw.Write((uint)ms.Length);
                bw.Write(ZlibCompress.Compress(ms));
                bw.WriteBE(checksum);
                ms.Dispose();
                bw.Flush();
                return fs.ToArray();
            }
        }
    }
}
