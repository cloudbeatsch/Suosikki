using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Configuration;
using Microsoft.WindowsAzure.Storage;
using Suosikki.WebJobs.Common;
using Newtonsoft.Json;

namespace Suosikki.WebJobs.CognitiveServices
{
    public class Functions
    {
        // TODO - add dependency injection for the API wrapper
        private static readonly RecommendationsApiWrapper recommender = new RecommendationsApiWrapper(
            ConfigurationManager.AppSettings["COG_SERVICES_API_KEY"].ToString());

        // queuename is defined in app.config
        public static void ProcessRequest(
            [QueueTrigger("%COG_UPLOAD_QUEUE%")] CogApiWebJobControlMsg request,
            CloudStorageAccount storageAccount)
        {
            switch (request.RequestType)
            {
                case CogApiWebJobControlMsg.RequestTypeEnum.Upload:
                    UploadFiles(request, storageAccount);
                    break;
                case CogApiWebJobControlMsg.RequestTypeEnum.Build:
                    BuildModel(request, storageAccount);
                    break;
                case CogApiWebJobControlMsg.RequestTypeEnum.Delete:
                    // we're not deleting models yet
                    break;
            }
        }

        private static void BuildModel(CogApiWebJobControlMsg request, CloudStorageAccount storageAccount)
        {
            var statsBlob = GetStatsBlob(request, storageAccount);
            try
            {
                long buildId = recommender.BuildRecommondationModel(request.ModelType, request.ModelType.ToString() + "_recommender_build");
                statsBlob.AppendText(JsonConvert.SerializeObject(new { Request = request, BuildId = buildId }) + Environment.NewLine);
            }
            catch (Exception ex)
            {
                statsBlob.AppendText(JsonConvert.SerializeObject(new { Request = request, Exception = ex }) + Environment.NewLine);
            }
        }

        internal delegate CatalogImportStats UploadFunction(string modelType, string filePath, string fileName);

        private static void UploadFiles(CogApiWebJobControlMsg request, CloudStorageAccount storageAccount)
        {
            var statsBlob = GetStatsBlob(request, storageAccount);
            // We need to upload the catalogue data before the usage data
            UploadFile(request, request.CatalogueBlobName, storageAccount, statsBlob, recommender.UploadCatalog);
            // now upload the usage data
            UploadFile(request, request.UsageBlobName, storageAccount, statsBlob, recommender.UploadUsage);
        }

        private static void UploadFile(CogApiWebJobControlMsg request, string blobName, CloudStorageAccount storageAccount, CloudAppendBlob statsBlob, UploadFunction uploadFunction)
        {
            string filePath = DownloadBlob(storageAccount, blobName);
            string fileName = Path.GetFileName(filePath);
            if (new FileInfo(filePath).Length > 0)
            {
                var stats = uploadFunction(request.ModelType, filePath, fileName);
                statsBlob.AppendText(JsonConvert.SerializeObject(stats) + Environment.NewLine);
            }
            File.Delete(fileName);
        }

        private static CloudAppendBlob GetStatsBlob(CogApiWebJobControlMsg request, CloudStorageAccount storageAccount)
        {
            var container = storageAccount.CreateCloudBlobClient().GetContainerReference(request.ContainerName);
            var statsBlob = container.GetAppendBlobReference(string.Format("stats/{0}.json", request.BatchId));
            if (!statsBlob.Exists())
            {
                statsBlob.CreateOrReplace();
            }

            return statsBlob;
        }

        /*
         * BlobAppends are not supported by Blob/Queue trigger in WebJob, requireing us to directly download the blobs
        */
        private static string DownloadBlob(CloudStorageAccount storageAccount, string blobName)
        {
            //Get create connect to the outbox blob
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // TODO: extract the container name
            CloudBlobContainer container = blobClient.GetContainerReference(
                ConfigurationManager.AppSettings["COG_INPUT_DATA_CONTAINER"].ToString()); 
            CloudAppendBlob blob = container.GetAppendBlobReference(blobName);

            String filePath = string.Format("{0}{1}.csv", Path.GetTempPath(), Guid.NewGuid().ToString());
            // only download the file if it doesn't already exist
            if (!File.Exists(filePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                blob.DownloadToFile(filePath, FileMode.CreateNew);
            }
            return filePath;
        }
    }
}
