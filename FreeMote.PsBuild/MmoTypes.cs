using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FreeMote.PsBuild
{
    /// <summary>
    /// MMO Meta Format for PSD
    /// </summary>
    public class MmoPsdMetadata
    {
        public string SourceLabel { get; set; }
        public string PsdComment { get; set; }
        public string PsdFrameLabel { get; set; }
        public string PsdGroup { get; set; }
        /// <summary>
        /// Category
        /// <para>WARNING: Category can not be set as `Expression`</para>
        /// </summary>
        public string Category { get; set; }
        public string Label { get; set; }
    }
}
