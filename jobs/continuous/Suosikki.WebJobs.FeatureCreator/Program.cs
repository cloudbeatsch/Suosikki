using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using System.Configuration;

namespace Suosikki.WebJobs.FeatureCreator
{
    class Program
    {
        static void Main()
        {
            var config = new JobHostConfiguration();
            config.NameResolver = new NameResolver();
            int value;
            config.Queues.BatchSize = 
                (int.TryParse(ConfigurationManager.AppSettings["QUEUE_BATCH_SIZE"], out value)) ? value : 16;
            var host = new JobHost(config);
            // The following code ensures that the WebJob will be running continuously
            host.RunAndBlock();
        }
    }
}
