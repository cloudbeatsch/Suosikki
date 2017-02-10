using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suosikki.WebJobs.CustomDefinitions
{
    public enum ModelTypeEnum { Music, Books, Videos };

    public class Config
    {
        public const char SEPERATOR = ',';
        public const string NA_STR = "NA";
    }
}
