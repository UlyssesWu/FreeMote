using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FreeMote.Plugins;
using FreeMote.Psb;
using Newtonsoft.Json;

namespace FreeMote.PsBuild
{
    /// <summary>
    /// Compile PSB File
    /// </summary>
    public static class PsbCompiler
    {
        /// <summary>
        /// Compile to file
        /// </summary>
        /// <param name="inputPath">Json file path</param>
        /// <param name="outputPath">Output path</param>
        /// <param name="inputResPath">Special resource Json file path</param>
        /// <param name="version">PSB version</param>
        /// <param name="cryptKey">CryptKey, if you need to use it outside FreeMote</param>
        /// <param name="platform">PSB Platform</param>
        /// <param name="renameOutput">If true, the output file extension is renamed by type</param>
        /// <param name="keepShell">If true, the output can be compressed PSB shell type (if specified)</param>
        public static void CompileToFile(string inputPath, string outputPath, string inputResPath = null,
            ushort? version = null, uint? cryptKey = null, PsbSpec? platform = null, bool renameOutput = true,
            bool keepShell = true)
        {
            if (string.IsNullOrEmpty(inputPath))
            {
                throw new FileNotFoundException("Can not find input json file.");
            }

            if (string.IsNullOrEmpty(inputResPath) || !File.Exists(inputResPath))
            {
                inputResPath = Path.ChangeExtension(inputPath, ".resx.json");
                if (!File.Exists(inputResPath))
                {
                    inputResPath = Path.ChangeExtension(inputPath, ".res.json");
                }
            }

            string resJson = null;
            string baseDir = Path.GetDirectoryName(inputPath);
            if (File.Exists(inputResPath))
            {
                resJson = File.ReadAllText(inputResPath);
                baseDir = Path.GetDirectoryName(inputResPath);
                if (renameOutput) //start renaming
                {
                    if (resJson.Trim().StartsWith("{"))
                    {
                        PsbResourceJson resx = JsonConvert.DeserializeObject<PsbResourceJson>(resJson);
                        bool pure = cryptKey == null && resx.CryptKey == null;
                        string ext = pure ? ".pure" : ".impure";
                        ext += resx.PsbType.HasValue ? resx.PsbType.Value.DefaultExtension() : ".psb";

                        if (resx.Context != null && resx.Context.ContainsKey(Consts.Context_PsbShellType) && keepShell)
                        {
                            var shellType = resx.Context[Consts.Context_PsbShellType] as string;
                            if (!string.IsNullOrEmpty(shellType) && shellType.ToUpperInvariant() != "PSB")
                            {
                                ext += $".{shellType.ToLowerInvariant()}";
                            }
                        }

                        var newPath = Path.ChangeExtension(outputPath, ext);
                        if (!string.IsNullOrWhiteSpace(newPath))
                        {
                            outputPath = newPath;
                        }
                    }
                }
            }

            var result = Compile(File.ReadAllText(inputPath), resJson, baseDir, version, cryptKey, platform, keepShell);

            // ReSharper disable once AssignNullToNotNullAttribute
            File.WriteAllBytes(outputPath, result);
        }

        /// <summary>
        /// Compile Json to PSB
        /// </summary>
        /// <param name="inputJson">Json text</param>
        /// <param name="inputResJson">Resource Json text</param>
        /// <param name="baseDir">If resource Json uses relative paths (usually it does), specify the base dir</param>
        /// <param name="version">PSB version</param>
        /// <param name="cryptKey">CryptKey, use null for pure PSB</param>
        /// <param name="spec">PSB Platform</param>
        /// <param name="keepShell">If true, try to compress PSB to shell type (MDF/LZ4 etc.) specified in resx.json; otherwise just output PSB</param>
        /// <returns></returns>
        public static byte[] Compile(string inputJson, string inputResJson, string baseDir = null,
            ushort? version = null, uint? cryptKey = null,
            PsbSpec? spec = null, bool keepShell = true)
        {
            var context = FreeMount.CreateContext();
            //Parse
            PSB psb = Parse(inputJson, version ?? 3);
            //Link
            if (!string.IsNullOrWhiteSpace(inputResJson))
            {
                if (inputResJson.Trim().StartsWith("{")) //resx.json
                {
                    PsbResourceJson resx = JsonConvert.DeserializeObject<PsbResourceJson>(inputResJson);
                    if (resx.PsbType != null)
                    {
                        psb.Type = resx.PsbType.Value;
                    }

                    if (resx.PsbVersion != null && version == null)
                    {
                        psb.Header.Version = resx.PsbVersion.Value;
                    }

                    if (resx.Platform != null && spec == null)
                    {
                        spec = resx.Platform;
                    }

                    if (resx.CryptKey != null & cryptKey == null)
                    {
                        cryptKey = resx.CryptKey;
                    }

                    context = FreeMount.CreateContext(resx.Context);

                    if (resx.ExternalTextures)
                    {
#if DEBUG
                        Console.WriteLine("[INFO] External Texture mode ON, no resource will be compiled.");
#endif
                    }
                    else
                    {
                        psb.Link(resx, baseDir);
                    }
                }
                else
                {
                    List<string> resources = JsonConvert.DeserializeObject<List<string>>(inputResJson);
                    psb.Link(resources, baseDir);
                }
            }

            //Build
            psb.Merge();
            if (spec != null && spec != psb.Platform)
            {
                psb.SwitchSpec(spec.Value, spec.Value.DefaultPixelFormat());
                psb.Merge();
            }

            var bytes = psb.Build();

            //Convert
            if (cryptKey != null)
            {
                return PsbFile.EncodeToBytes(cryptKey.Value, bytes, EncodeMode.Encrypt, EncodePosition.Auto);
            }

            if (context.HasShell && keepShell)
            {
                return context.PackToShell(new MemoryStream(bytes)).ToArray();
            }

            return bytes;
        }

        /// <summary>
        /// Load PSB and Context From Json file, use <see cref="LoadPsbFromJsonFile"/> if you don't need context
        /// </summary>
        /// <param name="inputPath">Json file path</param>
        /// <param name="inputResPath">Resource Json file</param>
        /// <param name="version">PSB version</param>
        /// <returns></returns>
        public static (PSB Psb, Dictionary<string, object> Context) LoadPsbAndContextFromJsonFile(string inputPath,
            string inputResPath = null,
            ushort? version = null)
        {
            if (string.IsNullOrEmpty(inputPath))
            {
                throw new FileNotFoundException("Can not find input json file.");
            }

            if (string.IsNullOrEmpty(inputResPath) || !File.Exists(inputResPath))
            {
                inputResPath = Path.ChangeExtension(inputPath, ".resx.json");
                if (!File.Exists(inputResPath))
                {
                    inputResPath = Path.ChangeExtension(inputPath, ".res.json");
                }
            }

            string inputResJson = null;
            string baseDir = Path.GetDirectoryName(inputPath);
            if (File.Exists(inputResPath))
            {
                inputResJson = File.ReadAllText(inputResPath);
                baseDir = Path.GetDirectoryName(inputPath);
            }

            //Parse
            PSB psb = Parse(File.ReadAllText(inputPath), version ?? 3);
            //Link
            Dictionary<string, object> context = null;
            if (!string.IsNullOrWhiteSpace(inputResJson))
            {
                if (inputResJson.Trim().StartsWith("{")) //resx.json
                {
                    PsbResourceJson resx = JsonConvert.DeserializeObject<PsbResourceJson>(inputResJson);
                    context = resx.Context;
                    if (resx.PsbType != null)
                    {
                        psb.Type = resx.PsbType.Value;
                    }

                    if (resx.PsbVersion != null && version == null)
                    {
                        psb.Header.Version = resx.PsbVersion.Value;
                    }

                    if (resx.ExternalTextures)
                    {
#if DEBUG
                        Console.WriteLine("[INFO] External Texture mode ON, no resource will be compiled.");
#endif
                    }
                    else
                    {
                        psb.Link(resx, baseDir);
                    }

                    if (resx.Platform != null)
                    {
                        psb.SwitchSpec(resx.Platform.Value, resx.Platform.Value.DefaultPixelFormat());
                    }
                }
                else
                {
                    List<string> resources = JsonConvert.DeserializeObject<List<string>>(inputResJson);
                    psb.Link(resources, baseDir);
                }
            }

            if (version != null)
            {
                psb.Header.Version = version.Value;
            }

            psb.Merge();
            return (psb, context);
        }

        /// <summary>
        /// Load PSB From Json file
        /// </summary>
        /// <param name="inputPath">Json file path</param>
        /// <param name="inputResPath">Resource Json file</param>
        /// <param name="version">PSB version</param>
        /// <returns></returns>
        public static PSB LoadPsbFromJsonFile(string inputPath, string inputResPath = null, ushort? version = null)
        {
            return LoadPsbAndContextFromJsonFile(inputPath, inputResPath, version).Psb;
        }

        internal static PSB Parse(string json, ushort version)
        {
            PSB psb = new PSB(version)
            {
                Objects = JsonConvert.DeserializeObject<PsbDictionary>(json, new PsbJsonConverter())
            };
            psb.InferType();
            psb.Collect(false, false); //don't merge res since it's empty now
            return psb;
        }

        /// <summary>
        /// Link Textures
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="resPaths">resource paths</param>
        /// <param name="baseDir"></param>
        /// <param name="order">how to arrange images</param>
        /// <param name="isExternal">Whether this is an external texture PSB</param>
        public static void Link(this PSB psb, IList<string> resPaths, string baseDir = null,
            PsbLinkOrderBy order = PsbLinkOrderBy.Convention, bool isExternal = false)
        {
            var context = FreeMount.CreateContext();

            if (psb.Type == PsbType.Motion)
            {
                PsbResHelper.LinkImages(psb, context, resPaths, baseDir, order, isExternal);
                return;
            }

            if (psb.TypeHandler != null)
            {
                psb.TypeHandler.Link(psb, context, resPaths, baseDir, order);
            }
            else
            {
                PsbResHelper.LinkImages(psb, context, resPaths, baseDir, order);
            }
        }

        /// <summary>
        /// Link
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="resx">advanced resource json(resx.jon)</param>
        /// <param name="baseDir"></param>
        internal static void Link(this PSB psb, PsbResourceJson resx, string baseDir)
        {
            if (resx.Resources == null)
            {
                return;
            }

            var context = FreeMount.CreateContext(resx.Context);
            if (psb.TypeHandler != null)
            {
                psb.TypeHandler.Link(psb, context, resx.Resources, baseDir);
            }
            else
            {
                PsbResHelper.LinkImages(psb, context, resx.Resources, baseDir);
            }
        }


        /// <summary>
        /// Modify the original PSB and only replace resources (according to json)
        /// </summary>
        /// <param name="psbPath">PSB to be modified</param>
        /// <param name="jsonPath">PSB Json which only resources are changed</param>
        /// <returns></returns>
        public static MemoryStream InplaceReplace(string psbPath, string jsonPath)
        {
            var jsonPsb = LoadPsbFromJsonFile(jsonPath);
            using var psbFs = File.OpenRead(psbPath);

            var ctx = FreeMount.CreateContext();
            var psbStream = ctx.OpenStreamFromPsbFile(psbPath);
            var psb = new PSB(psbStream);

            if (jsonPsb.Resources.Count != psb.Resources.Count)
            {
                throw new NotSupportedException("The 2 PSBs are different (Resource count).");
            }

            MemoryStream ms = new MemoryStream((int)psbStream.Length);
            psbStream.Seek(0, SeekOrigin.Begin);
            psbStream.CopyTo(ms);
            using BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8, true);

            for (var i = 0; i < jsonPsb.Resources.Count; i++)
            {
                var resource = jsonPsb.Resources[i];
                var oriResource = psb.Resources[i];
                if (resource.Data.Length > oriResource.Data.Length)
                {
                    throw new NotSupportedException($"The 2 PSBs are different (Resource {i} length: {resource.Data.Length} vs {oriResource.Data.Length}).");
                }

                if (oriResource.Index == null)
                {
                    Console.WriteLine($"[WARN] Resource {i} is not replaced.");
                    continue;
                }

                var offset = psb.ChunkOffsets[(int)oriResource.Index];
                var length = psb.ChunkLengths[(int)oriResource.Index];

                bw.BaseStream.Seek(psb.Header.OffsetChunkData + offset, SeekOrigin.Begin);
                bw.Write(resource.Data);
                if (length > resource.Data.Length)
                {
                    bw.Write(new byte[length - resource.Data.Length]);
                }
            }
            
            return ms;
        }

        /// <summary>
        /// <inheritdoc cref="InplaceReplace"/>
        /// </summary>
        public static string InplaceReplaceToFile(string psbPath, string jsonPath)
        {
            var ms = InplaceReplace(psbPath, jsonPath);
            var outputPath = Path.ChangeExtension(psbPath, "IR.psb");
            using var fs = File.Create(outputPath);
            ms.WriteTo(fs);
            fs.Close();
            ms.Close();
            return outputPath;
        }
    }
}