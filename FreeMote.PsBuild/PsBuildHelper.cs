using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeMote.Psb;

namespace FreeMote.PsBuild
{
    public static class PsBuildHelper
    {
        public static PsbNumber ToPsbNumber(this MmoMarkerColor color)
        {
            return ((int) color).ToPsbNumber();
        }
    }
}
