using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeMote.Psb;

namespace FreeMote.PsBuild.SpecConverters
{
    /// <summary>
    /// Spec Converter
    /// </summary>
    interface ISpecConverter
    {
        /// <summary>
        /// Convert a PSB to target spec
        /// </summary>
        /// <param name="psb"></param>
        void Convert(PSB psb);

        PsbPixelFormat TargetPixelFormat { get; set; }
        bool UseRL { get; set; }

        PsbSpec FromSpec { get; }
        PsbSpec ToSpec { get; }
    }
}
