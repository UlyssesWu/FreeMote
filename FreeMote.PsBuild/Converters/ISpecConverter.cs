using System.Collections.Generic;
using FreeMote.Psb;

namespace FreeMote.PsBuild.Converters
{
    /// <summary>
    /// Spec convert strategy
    /// </summary>
    public enum SpecConvertOption
    {
        /// <summary>
        /// Best success rate
        /// </summary>
        Default,
        /// <summary>
        /// Remove unnecessary info
        /// </summary>
        Minimum,
        /// <summary>
        /// Keep unnecessary info
        /// </summary>
        Maximum,
    }

    /// <summary>
    /// Convert among <see cref="PsbSpec"/>
    /// </summary>
    public interface ISpecConverter
    {
        /// <summary>
        /// Convert a PSB to target <see cref="PsbSpec"/>
        /// </summary>
        /// <param name="psb"></param>
        void Convert(PSB psb);

        SpecConvertOption ConvertOption { get; set; }

        /// <summary>
        /// Select <see cref="PsbPixelFormat"/> if supported
        /// </summary>
        PsbPixelFormat TargetPixelFormat { get; set; }

        /// <summary>
        /// Use RL Compress
        /// </summary>
        bool UseRL { get; set; }

        IList<PsbSpec> FromSpec { get; }
        IList<PsbSpec> ToSpec { get; }
    }
}
