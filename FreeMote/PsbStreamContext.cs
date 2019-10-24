namespace FreeMote
{
    /// <summary>
    /// PSB Stream Cipher Context (XorShift128)
    /// </summary>
    public class PsbStreamContext
    {
        /// <summary>
        /// Key 1
        /// <para>Usually you shouldn't modify it.</para>
        /// </summary>
        public uint Key1 { get; set; }
        /// <summary>
        /// Key 2
        /// <para>Usually you shouldn't modify it.</para>
        /// </summary>
        public uint Key2 { get; set; }
        /// <summary>
        /// Key 3
        /// <para>Usually you shouldn't modify it.</para>
        /// </summary>
        public uint Key3 { get; set; }
        /// <summary>
        /// Key 4
        /// <para>This is the key which differs among versions.</para>
        /// </summary>
        public uint Key4 { get; set; }

        /// <summary>
        /// Current Key
        /// </summary>
        public uint CurrentKey { get; internal set; } = 0;
        /// <summary>
        /// Stream Round
        /// </summary>
        public uint Round { get; internal set; }
        /// <summary>
        /// Round Count in byte
        /// </summary>
        public ulong ByteCount { get; internal set; }

        public PsbStreamContext()
        {
            Init();
        }

        public PsbStreamContext(uint key)
        {
            Init();
            Key4 = key;
        }

        public void Init()
        {
            Round = 0;
            ByteCount = 0;
            Key1 = Consts.Key1;
            Key2 = Consts.Key2;
            Key3 = Consts.Key3;
            CurrentKey = 0;
        }

        /// <summary>
        /// Encode bytes using stream cipher.
        /// <para>Every time called, the key might be modified.</para>
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public ref byte[] Encode(ref byte[] input)
        {
            for (int i = 0; i < input.Length; i++)
            {
                if (CurrentKey == 0)
                {
                    var a = Key1 ^ (Key1 << 11);
                    var b = Key4;
                    var c = a ^ b ^ ((a ^ (b >> 11)) >> 8);
                    Key1 = Key2;
                    Key2 = Key3;
                    Key3 = b;
                    Key4 = c;
                    CurrentKey = c;
                    Round++;
                }
                input[i] ^= (byte) CurrentKey;
                CurrentKey = CurrentKey >> 8;
                ByteCount++;
            }
            return ref input;
        }

        /// <summary>
        /// Encode bytes using stream cipher.
        /// <para>Every time called, the key might be modified.</para>
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public byte[] Encode(byte[] input)
        {
            for (int i = 0; i < input.Length; i++)
            {
                if (CurrentKey == 0)
                {
                    var a = Key1 ^ (Key1 << 11);
                    var b = Key4;
                    var c = a ^ b ^ ((a ^ (b >> 11)) >> 8);
                    Key1 = Key2;
                    Key2 = Key3;
                    Key3 = b;
                    Key4 = c;
                    CurrentKey = c;
                    Round++;
                }
                input[i] ^= (byte)CurrentKey;
                CurrentKey = CurrentKey >> 8;
                ByteCount++;
            }
            return input;
        }

        /// <summary>
        /// Skip some bytes
        /// </summary>
        /// <param name="byteLength"></param>
        public void FastForward(uint byteLength)
        {
            for (int i = 0; i < byteLength; i++)
            {
                if (CurrentKey == 0)
                {
                    var a = Key1 ^ (Key1 << 11);
                    var b = Key4;
                    var c = a ^ b ^ ((a ^ (b >> 11)) >> 8);
                    Key1 = Key2;
                    Key2 = Key3;
                    Key3 = b;
                    Key4 = c;
                    CurrentKey = c;
                    Round++;
                }
                CurrentKey = CurrentKey >> 1;
                ByteCount++;
            }
        }

        /// <summary>
        /// Skip to next round
        /// </summary>
        public uint NextRound()
        {
            while (CurrentKey != 0)
            {
                CurrentKey = CurrentKey >> 1;
                ByteCount++;
            }
            var a = Key1 ^ (Key1 << 11);
            var b = Key4;
            var c = a ^ b ^ ((a ^ (b >> 11)) >> 8);
            Key1 = Key2;
            Key2 = Key3;
            Key3 = b;
            Key4 = c;
            CurrentKey = c;
            Round++;
            return CurrentKey;
        }
    }
}
