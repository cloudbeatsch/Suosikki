using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Suosikki.WebJobs.UsageLoader
{
    class Program
    {
        static void Main()
        {
            JobHostConfiguration config = new JobHostConfiguration();
            config.NameResolver = new NameResolver();
            var host = new JobHost(config);
            host.Call(typeof(Functions).GetMethod("ImportSuosikkiData"));
        }
    }
}
