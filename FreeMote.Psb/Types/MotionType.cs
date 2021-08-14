using System.Collections.Generic;
using System.Linq;
using FreeMote.Plugins;
using static FreeMote.Psb.PsbResHelper;

namespace FreeMote.Psb.Types
{
    class MotionType : BaseImageType, IPsbType
    {
        public const string MotionSourceKey = "source";

        public PsbType PsbType => PsbType.Motion;
        public bool IsThisType(PSB psb)
        {
            return psb.TypeId == "motion";
        }

        public List<T> CollectResources<T>(PSB psb, bool deDuplication = true) where T : IResourceMetadata
        {
            List<T> resourceList = psb.Resources == null
                ? new List<T>()
                : new List<T>(psb.Resources.Count);

            if (psb.Objects != null && psb.Objects.ContainsKey(MotionSourceKey))
            {
                FindMotionResources(resourceList, psb.Objects[MotionSourceKey], deDuplication);
            }

            resourceList.ForEach(r =>
            {
                r.PsbType = psb.Type;
                r.Spec = psb.Platform;
            });

            return resourceList;
        }

        public override void Link(PSB psb, FreeMountContext context, IList<string> resPaths, string baseDir = null,
            PsbLinkOrderBy order = PsbLinkOrderBy.Convention)
        {
            LinkImages(psb, context, resPaths, baseDir, order, true);
        }
        
        private static void FindMotionResources<T>(List<T> list, IPsbValue obj, bool deDuplication = true) where T: IResourceMetadata
        {
            switch (obj)
            {
                case PsbList c:
                    c.ForEach(o => FindMotionResources(list, o, deDuplication));
                    break;
                case PsbDictionary d:
                    if (d[Consts.ResourceKey] is PsbResource r)
                    {
                        if (!deDuplication)
                        {
                            list.Add((T)(IResourceMetadata)GenerateImageMetadata(d, r));
                        }
                        else if (r.Index == null || list.FirstOrDefault(md => md.Index == r.Index.Value) == null)
                        {
                            list.Add((T)(IResourceMetadata)GenerateImageMetadata(d, r));
                        }
                    }

                    foreach (var o in d.Values)
                    {
                        FindMotionResources(list, o, deDuplication);
                    }

                    break;
            }
        }

        /// <summary>
        /// Add stub <see cref="PsbResource"/> to this PSB
        /// </summary>
        /// <param name="psb"></param>
        internal static List<PsbResource> MotionResourceInstrument(PSB psb)
        {
            if (!psb.Objects.ContainsKey(MotionSourceKey))
            {
                return null;
            }

            var resources = new List<PsbResource>();
            GenerateMotionResourceStubs(resources, psb.Objects[MotionSourceKey]);
            return resources;
        }

        /// <summary>
        /// Add stubs (<see cref="PsbResource"/> with null Data) into a Motion PSB. A stub must be linked with a texture, or it will be null after <see cref="PSB.Build"/>
        /// </summary>
        /// <param name="resources"></param>
        /// <param name="obj"></param>
        private static void GenerateMotionResourceStubs(List<PsbResource> resources, IPsbValue obj)
        {
            switch (obj)
            {
                case PsbList c:
                    c.ForEach(o => GenerateMotionResourceStubs(resources, o));
                    break;
                case PsbDictionary d:
                    if (d.ContainsKey(Consts.ResourceKey) && (d[Consts.ResourceKey] == null || d[Consts.ResourceKey] is PsbNull))
                    {
                        if (d.ContainsKey("width") && d.ContainsKey("height"))
                        {
                            //confirmed, add stub
                            PsbResource res = new PsbResource();
                            resources.Add(res);
                            res.Index = (uint)resources.IndexOf(res);
                            d[Consts.ResourceKey] = res;
                        }
                    }

                    foreach (var o in d.Values)
                    {
                        GenerateMotionResourceStubs(resources, o);
                    }

                    break;
            }
        }

        public override Dictionary<string, string> OutputResources(PSB psb, FreeMountContext context, string name, string dirPath,
            PsbExtractOption extractOption = PsbExtractOption.Original)
        {
            return base.OutputResources(psb, context, name, dirPath, extractOption);
        }
    }
}
