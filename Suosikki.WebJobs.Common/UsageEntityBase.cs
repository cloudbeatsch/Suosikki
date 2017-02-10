using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suosikki.WebJobs.Common
{
    public abstract class UsageEntityBase
    {
        public UsageEntityBase()
        {

        }
        // parses the input line and initializes the properties
        public abstract void ParsLine(string line);
        public string UserId { get; set; }
        public string ItemId { get; set; }
        public string DateTime { get; set; }
        public int Rating { get; set; }
        public bool IsValid { get; set; }
        public string ModelType { get; set; }

        public abstract ModelProcessorBase CreateModelProcessor<TUsage_Entity>(ModelProcessorCreationMsg creationMsg);
    }
}
