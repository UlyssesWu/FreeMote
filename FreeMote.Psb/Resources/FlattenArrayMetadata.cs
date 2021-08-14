using System;
using System.IO;
using System.Runtime.InteropServices;
using FreeMote.Plugins;

namespace FreeMote.Psb
{
    /// <summary>
    /// Information about FlattenArray such as rawMeshList
    /// </summary>
    public class FlattenArrayMetadata : IResourceMetadata
    {
        public string Name { get; set; }
        public PsbResource Resource { get; set; }

        public uint Index
        {
            get => Resource.Index ?? uint.MaxValue;
            set
            {
                if (Resource != null)
                {
                    Resource.Index = value;
                }
            }
        }

        public byte[] Data
        {
            get => Resource?.Data;

            internal set
            {
                if (Resource == null)
                {
                    throw new NullReferenceException("Resource is null");
                }

                Resource.Data = value;
            }
        }

        public Span<float> FloatValues => MemoryMarshal.Cast<byte, float>(Data.AsSpan());

        public PsbSpec Spec { get; set; }
        public PsbType PsbType { get; set; }

        public void Link(string fullPath, FreeMountContext context)
        {
            Data = File.ReadAllBytes(fullPath);
        }
    }
}
