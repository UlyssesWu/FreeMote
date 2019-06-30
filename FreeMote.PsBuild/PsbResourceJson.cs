using System;
using System.Collections.Generic;
using FreeMote.Plugins;
using FreeMote.Psb;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FreeMote.PsBuild
{
    /// <summary>
    /// Advanced resource json (.resx.json)
    /// </summary>
    /// JsonConvert.SerializeObject(MyObject, new Newtonsoft.Json.Converters.StringEnumConverter());
    [Serializable]
    public class PsbResourceJson
    {
        /// <summary>
        /// PSB version
        /// </summary>
        public ushort? PsbVersion { get; set; } = 3;

        /// <summary>
        /// PSB Type
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public PsbType? PsbType { get; set; } = null;

        /// <summary>
        /// PSB Spec (only for EMT)
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public PsbSpec? Platform { get; set; } = null;

        /// <summary>
        /// Key
        /// </summary>
        public uint? CryptKey { get; set; } = null;

        /// <summary>
        /// Whether to use external textures
        /// </summary>
        public bool ExternalTextures { get; set; } = false;
        
        /// <summary>
        /// Setting Context (mainly for plugins)
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Resources
        /// </summary>
        public Dictionary<string, string> Resources { get; set; }

        public PsbResourceJson()
        {
        }

        public PsbResourceJson(PSB psb, Dictionary<string, object> context = null)
        {
            PsbVersion = psb.Header.Version;
            PsbType = psb.Type;
            Platform = psb.Platform;
            ExternalTextures = psb.Type == FreeMote.PsbType.Motion && psb.Resources.Count <= 0;

            if (context != null)
            {
                CryptKey = context.ContainsKey(FreeMount.CryptKey)
                    ? (uint?) context[FreeMount.CryptKey]
                    : null;
                Context = context;
            }
        }

        public string SerializeToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
