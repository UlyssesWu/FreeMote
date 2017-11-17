using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeMote.Psb;

namespace FreeMote.PsBuild.SpecConverters
{
    /// <summary>
    /// Useless
    /// </summary>
    class Common2WinConverter : ISpecConverter
    {
        public void Convert(PSB psb)
        {
            throw new NotImplementedException();
        }

        public SpecConvertOption ConvertOption { get; set; }


        public PsbPixelFormat TargetPixelFormat { get; set; }
        public bool UseRL { get; set; }
        public PsbSpec FromSpec { get; }
        public PsbSpec ToSpec { get; }
    }
}
