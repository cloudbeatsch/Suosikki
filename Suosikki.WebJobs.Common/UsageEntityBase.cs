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
        // parses a single usage line and initializes the properties
        public abstract void ParsLine(string line);
        // true if the usage line was succesfully parsed
        public bool IsValid { get; set; }

        public string UserId { get; set; }
        public string ItemId { get; set; }
        public string DateTime { get; set; }
        // rating as a value between 1 - (<100)
        public int Rating { get; set; }
        // specifing the destination model (e.g. video, music, books, ...)
        public string ModelType { get; set; }

        // creates and returns a ModelProcessor to process an instance of type ModelType.
        public abstract ModelProcessorBase CreateModelProcessor<TUsage_Entity>(ModelProcessorCreationMsg creationMsg);
    }
}
