using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using System;
using System.Configuration;

namespace Suosikki.WebJobs.CognitiveServices
{  

    class Program
    {
        static void Main()
        {
            JobHostConfiguration config = new JobHostConfiguration();
            config.NameResolver = new QueueNameResolver();
            // needs to be a singleton and we process only one message at the time
            config.Queues.BatchSize = 1;
            var host = new JobHost(config); host.RunAndBlock();
        }
        
    }
}
