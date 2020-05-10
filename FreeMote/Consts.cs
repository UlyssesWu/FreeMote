using System.Collections.Generic;
using System.Linq;
using System.Text;

// ReSharper disable InconsistentNaming

namespace FreeMote
{
    public static class Consts
    {
        /// <summary>
        /// (string)
        /// </summary>
        public const string Context_PsbShellType = "PsbShellType";

        /// <summary>
        /// (uint?)
        /// </summary>
        public const string Context_CryptKey = "CryptKey";

        /// <summary>
        /// (bool)
        /// <para>Fast: 0x9C BestCompression: 0xDA NoCompression/Low: 0x01</para>
        /// </summary>
        public const string Context_PsbZlibFastCompress = "PsbZlibFastCompress";

        /// <summary>
        /// (List) Archive sources
        /// </summary>
        public const string Context_ArchiveSource = "ArchiveSource";

        /// <summary>
        /// (string) MDF Seed (key + filename)
        /// </summary>
        public const string Context_MdfKey = "MdfKey";

        /// <summary>
        /// (string) MDF Key for MT19937
        /// </summary>
        public const string Context_MdfMtKey = "MdfMtKey";

        /// <summary>
        /// (int) MDF Key length
        /// </summary>
        public const string Context_MdfKeyLength = "MdfKeyLength";

        /// <summary>
        /// 0x075BCD15
        /// </summary>
        public const uint Key1 = 123456789;

        /// <summary>
        /// 0x159A55E5
        /// </summary>
        public const uint Key2 = 362436069;

        /// <summary>
        /// 0x1F123BB5
        /// </summary>
        public const uint Key3 = 521288629;

        /// <summary>
        /// Perform 16 byte data align or not (when build)
        /// </summary>
        public static bool PsbDataStructureAlign { get; set; } = true;

        public static Encoding PsbEncoding { get; set; } = Encoding.UTF8;

        /// <summary>
        /// Take more memory when loading, but maybe faster
        /// </summary>
        public static bool InMemoryLoading { get; set; } = true;

        /// <summary>
        /// Use more inferences to make loading fast, set to False when something is wrong
        /// </summary>
        public static bool FastMode { get; set; } = true;

        /// <summary>
        /// Use hex numbers in json to keep all float numbers correct
        /// </summary>
        public static bool JsonUseHexNumber { get; set; } = false;

        /// <summary>
        /// Collapse arrays in json
        /// </summary>
        public static bool JsonArrayCollapse { get; set; } = true;

        /// <summary>
        /// Always use double instead of float
        /// </summary>
        public static bool JsonUseDoubleOnly { get; set; } = false;

        /// <summary>
        /// (not implemented yet) Use Palette Merge will increase compile time but cut output size (only when using CI* images)
        /// </summary>
        public static bool PaletteMerge { get; set; } = false;

        /// <summary>
        /// Allows you to edit CI* images by re-generate the palette for each bppIndexed image (will increase size), otherwise you should not change those images
        /// </summary>
        public static bool GeneratePalette { get; set; } = true;

        /// <summary>
        /// If true, prefer plugins than inherit
        /// </summary>
        public static bool PluginFirst { get; set; } = false;
    }

    //REF: https://stackoverflow.com/a/24987840/4374462
    public static class ListExtras
    {
        //    list: List<T> to resize
        //    size: desired new size
        // element: default value to insert

        public static void Resize<T>(this List<T> list, int size, T element = default(T))
        {
            int count = list.Count;

            if (size < count)
            {
                list.RemoveRange(size, count - size);
            }
            else if (size > count)
            {
                if (size > list.Capacity) // Optimization
                    list.Capacity = size;

                list.AddRange(Enumerable.Repeat(element, size - count));
            }
        }

        public static void EnsureSize<T>(this List<T> list, int size, T element = default(T))
        {
            if (list.Count < size)
            {
                list.Resize(size, element);
            }
        }

        public static void Set<T>(this List<T> list, int index, T value, T defaultValue = default(T))
        {
            if (list.Count < index + 1)
            {
                list.Resize(index + 1, defaultValue);
            }

            list[index] = value;
        }
    }
}