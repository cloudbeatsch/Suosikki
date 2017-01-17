using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Configuration;
using Suosikki.WebJobs.CustomDefinitions;
using Suosikki.WebJobs.Common;
using System.Text.RegularExpressions;
namespace Suosikki.WebJobs.FeatureCreator
{
    public class Functions
    {
        public static void CreateSuosikkiFeatures(
            [QueueTrigger("%ADD_MODEL_DATA_QUEUE%")] string newDataLines,
            [Queue("%SINGLE_COG_UPLOAD_QUEUE%")] ICollector<SingleUploadMsg> uploadQueue,
            CloudStorageAccount storageAccount,
            TextWriter log)
        {
            var blobClient = storageAccount.CreateCloudBlobClient();
            var cogInputDataContainer = blobClient.GetContainerReference(
            ConfigurationManager.AppSettings["COG_INPUT_DATA_CONTAINER"]);
            cogInputDataContainer.CreateIfNotExists();

            bool queryUserFeatures = Boolean.Parse(
                ConfigurationManager.AppSettings["QUERY_USER_FEATURES"]);
            bool queryItemFeatures = Boolean.Parse(
                ConfigurationManager.AppSettings["QUERY_ITEM_FEATURES"]);

            // We create ModelProcessorCollection 
            IModelProcessorCollection modelProcessors = new ModelProcessorCollection<UsageEntity>(storageAccount.CreateCloudTableClient(), cogInputDataContainer, log, queryUserFeatures, queryItemFeatures);

            foreach (var newData in newDataLines.Replace("\n", "").Split('\r'))
            {
                try
                {
                    modelProcessors.ProcessUsageLine(newData);
                }
                catch (Exception ex)
                {
                    log.WriteLine("line {0} caused excpetion {1}", newData, ex.Message);
                    throw ex;
                }
            }
            // create the upload message for the models
            modelProcessors.CreateUploadMessages(uploadQueue);
        }
    }
}
