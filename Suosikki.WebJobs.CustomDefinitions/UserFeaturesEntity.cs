using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suosikki.WebJobs.CustomDefinitions
{
    public class UserFeaturesEntity : TableEntity
    {
        public string Gender { get; set; } = Config.NA_STR;
        public string Country { get; set; } = Config.NA_STR;
        public string Birthday { get; set; } = Config.NA_STR;
    }
}
