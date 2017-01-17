using Microsoft.Azure.WebJobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suosikki.WebJobs.Common
{
    public interface IModelProcessorCollection
    {
        void ProcessUsageLine(string usageData);
        void CreateUploadMessages(ICollector<SingleUploadMsg> uploadQueue);
    }
}
