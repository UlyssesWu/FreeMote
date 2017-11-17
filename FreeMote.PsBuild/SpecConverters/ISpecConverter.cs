using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeMote.Psb;

namespace FreeMote.PsBuild.SpecConverters
{
    public enum SpecConvertOption
    {
        /// <summary>
        /// Minimum error
        /// </summary>
        Default,
        /// <summary>
        /// Remove unnecessary info
        /// </summary>
        Minimum,
        /// <summary>
        /// Keep unnecessary info
        /// </summary>
        Maximum
    }

    /// <summary>
    /// Spec Converter
    /// </summary>
    public interface ISpecConverter
    {
        /// <summary>
        /// Convert a PSB to target spec
        /// </summary>
        /// <param name="psb"></param>
        void Convert(PSB psb);

        SpecConvertOption ConvertOption { get; set; }
        PsbPixelFormat TargetPixelFormat { get; set; }
        bool UseRL { get; set; }

        PsbSpec FromSpec { get; }
        PsbSpec ToSpec { get; }
    }
}
