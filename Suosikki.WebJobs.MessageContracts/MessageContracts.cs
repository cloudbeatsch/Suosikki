using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suosikki.WebJobs.Common
{
    public class CogApiWebJobControlMsg
    {
        public enum RequestTypeEnum { Upload, Build, Delete };

        public string ModelType { get; set; }
        public RequestTypeEnum RequestType { get; set; }
        public string UsageBlobName { get; set; }
        public string CatalogueBlobName { get; set; }
        public string BatchId { get; set; }
        public string ContainerName { get; set; }
    }
    public class SingleUploadMsg
    {
        public string ModelType { get; set; }
        public string UsageBlobName { get; set; }
        public string CatalogueBlobName { get; set; }
    }
}
