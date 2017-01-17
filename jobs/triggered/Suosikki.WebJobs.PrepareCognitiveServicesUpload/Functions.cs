using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Suosikki.WebJobs.Common;
using Microsoft.WindowsAzure.Storage;
using System.Configuration;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace Suosikki.WebJobs.PrepareCognitiveServicesUpload
{
    public class OutputBlob
    {
        // limit the blob size to 100 MB
        const long MAX_BLOB_SIZE = 100 * 1024 * 1024;
        private CloudBlobContainer container;
        private string blobPath;
        public long Size  = 0;
        private CloudAppendBlob blob;
        private OutputBlobs blobCollection;

        public string Name { get; set; }

        public OutputBlob(OutputBlobs owner, CloudBlobContainer cogInputDataContainer, string path)
        {
            container = cogInputDataContainer;
            blobCollection = owner;
            blobPath = path;
            CreateNewBlob();
        }
        private void CreateNewBlob()
        {
            blobCollection.AddMessage();
            Name = blobPath + Guid.NewGuid() + ".csv";
            blob = container.GetAppendBlobReference(Name);
            blob.CreateOrReplace();
            Size = 0;
        }
        public void Append(byte[] bytes)
        {
            Size += bytes.Length;
            if (Size > MAX_BLOB_SIZE)
            {
                CreateNewBlob();
            }
            blob.AppendFromByteArray(bytes, 0, bytes.Length);
        }
        public void Append(string text)
        {
            Size += text.Length*2;
            if (Size > MAX_BLOB_SIZE)
            {
                CreateNewBlob();
            }
            blob.AppendText(text + Environment.NewLine);
        }
    }
    public class OutputBlobs
    {
        private ICollector<CogApiWebJobControlMsg> aggregatedUploadQueue;
        public string ModelType { get; set; }
        private string batchId;
        private string containerName;
        public OutputBlobs(CloudBlobContainer cogInputDataContainer, ICollector<CogApiWebJobControlMsg> queue,
                           string cogContainerName, string uploadId, string contentType)
        {
            aggregatedUploadQueue = queue;
            ModelType = contentType;
            batchId = uploadId;
            containerName = cogContainerName;
            CatalogueBlob = new OutputBlob(this, cogInputDataContainer, 
                string.Format("aggregated/{0}/{1}/catalogue/", uploadId, ModelType.ToString()));
            UsageBlob = new OutputBlob(this, cogInputDataContainer,
                string.Format("aggregated/{0}/{1}/usage/", uploadId, ModelType.ToString()));
        }
        public void AddMessage()
        {
            if (CatalogueBlob == null || UsageBlob == null)
            {
                return;
            }
            // only add a message if the blob contains content
            if (CatalogueBlob.Size > 0 || (UsageBlob.Size > 0))
            {
                aggregatedUploadQueue.Add(new CogApiWebJobControlMsg()
                {
                    BatchId = batchId,
                    ContainerName = containerName,
                    CatalogueBlobName = CatalogueBlob.Name,
                    UsageBlobName = UsageBlob.Name,
                    ModelType = ModelType
                });
            }
        }
        public OutputBlob CatalogueBlob { get; set; }
        public OutputBlob UsageBlob { get; set; }
    }
    public class Functions
    {
        public static void AggregateRequestMessages(
            [Queue("%SINGLE_COG_UPLOAD_QUEUE%")] CloudQueue singleUploadsQueue,
            [Queue("%COG_UPLOAD_QUEUE%")] ICollector<CogApiWebJobControlMsg> aggregatedUploads,
            CloudStorageAccount storageAccount,
            TextWriter log)
        {
            var blobClient = storageAccount.CreateCloudBlobClient();
            string containerName = ConfigurationManager.AppSettings["COG_INPUT_DATA_CONTAINER"].ToString();
            var cogInputDataContainer = blobClient.GetContainerReference(containerName);
            cogInputDataContainer.CreateIfNotExists();

            string uploadId = DateTime.Now.Ticks.ToString();
            var blobs = new Dictionary<string, OutputBlobs>();

            const int BATCH_SIZE = 32;
            // retrieve the messages with a timeout of 10 minutes
            var timeout = new TimeSpan(0, 10, 0);
            var singleUploadMsgs = singleUploadsQueue.GetMessages(BATCH_SIZE, timeout);
            var catalogueCache = new Dictionary<string, byte>();
            while (singleUploadMsgs.Count() > 0)
            {
                foreach (var msg in singleUploadMsgs)
                {
                    var upload = JsonConvert.DeserializeObject<SingleUploadMsg>(msg.AsString);
                    if (!blobs.ContainsKey(upload.ModelType))
                    {
                        blobs[upload.ModelType] = new OutputBlobs(cogInputDataContainer, aggregatedUploads, containerName, uploadId, upload.ModelType);
                    }
                    AppendBlobContent(cogInputDataContainer, upload.UsageBlobName, blobs[upload.ModelType].UsageBlob);
                    UpdateCatalogue(cogInputDataContainer, upload.CatalogueBlobName, catalogueCache, blobs[upload.ModelType].CatalogueBlob);
                    singleUploadsQueue.DeleteMessage(msg);
                    log.WriteLine("Finished processing blob {0} {1}", upload.CatalogueBlobName, upload.UsageBlobName);
                }
                singleUploadMsgs = singleUploadsQueue.GetMessages(BATCH_SIZE, timeout);
            }
            // add the remaining blob names to the queue
            foreach (var blob in blobs.Values)
            {
                blob.AddMessage();
            }
            // add the build request to the queue 
            foreach (var model in blobs.Keys)
            {
                aggregatedUploads.Add(new CogApiWebJobControlMsg()
                { 
                    BatchId = uploadId,
                    ContainerName = containerName,
                    ModelType = model,
                    RequestType = CogApiWebJobControlMsg.RequestTypeEnum.Build
                });
            }
        }

        private static void UpdateCatalogue(CloudBlobContainer sourceContainer, string catalogueBlobName, Dictionary<string, byte> catalogue, OutputBlob destinationBlob)
        {
            var sourceBlob = sourceContainer.GetBlobReference(catalogueBlobName);
            using (var memoryStream = new MemoryStream())
            {
                sourceBlob.DownloadToStream(memoryStream);
                using (var reader = new StringReader(System.Text.Encoding.UTF8.GetString(memoryStream.ToArray())))
                {
                    string line = null;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string itemId = line.Split(',')[0];
                        if (!catalogue.ContainsKey(itemId))
                        {
                            destinationBlob.Append(line);
                            catalogue[itemId]=0;
                        }
                    }
                }
            }
            sourceBlob.Delete();
        }

        private static void AppendBlobContent(CloudBlobContainer sourceContainer, string sourceBlobName, OutputBlob destinationBlob)
        {
            var sourceBlob = sourceContainer.GetBlobReference(sourceBlobName);
            using (var memoryStream = new MemoryStream())
            {
                sourceBlob.DownloadToStream(memoryStream);
                var data = memoryStream.GetBuffer();
                destinationBlob.Append(data);
            }
            sourceBlob.Delete();
        }
    }
}
