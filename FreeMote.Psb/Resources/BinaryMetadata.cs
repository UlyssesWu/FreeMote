using System;
using System.IO;
using FreeMote.Plugins;

namespace FreeMote.Psb
{
    public class BinaryMetadata : IResourceMetadata
    {
        public string Name { get; set; }
        public uint Index { get; }
        public PsbSpec Spec { get; set; }
        public PsbType PsbType { get; set; }
        public PsbResource Resource { get; set; }


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


        public void Link(string fullPath, FreeMountContext context)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                if (Consts.StrictMode)
                {
                    throw new FileNotFoundException("[ERROR] Cannot find file to Link.", fullPath);
                }
                else
                {
                    Logger.LogWarn($"[WARN] Cannot find file to Link at {fullPath}.");
                }

                return;
            }

            Resource = new PsbResource() { Data = File.ReadAllBytes(fullPath) };
        }
    }
}
