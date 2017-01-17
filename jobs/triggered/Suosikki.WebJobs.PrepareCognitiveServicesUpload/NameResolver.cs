using Microsoft.Azure.WebJobs;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suosikki.WebJobs.PrepareCognitiveServicesUpload
{
    public class NameResolver : INameResolver
    {
        public string Resolve(string name)
        {
            return ConfigurationManager.AppSettings[name].ToString();
        }
    }
}
