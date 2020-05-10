using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeMote.Psb.Types;

namespace FreeMote.Psb
{
    interface IPsbType
    {
        PsbType PsbType { get; }

        bool IsThisType(PSB psb);
    }
}
