using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suosikki.WebJobs.Common
{
    public class UserItemRatingEntity : TableEntity
    {
        public int Rating { get; set; }
    }
}
