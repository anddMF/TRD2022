using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Trade02.Infra.Cross
{
    public static class Utils
    {
        public static string FormatDecimal(decimal toTransform)
        {
            return String.Format("{0:0.00}", toTransform);
        }
    }
}