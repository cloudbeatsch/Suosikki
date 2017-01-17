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

namespace Suosikki.WebJobs.UsageLoader
{
    public class Config
    {
        public const int MESSAGE_SIZE = 64 * 1024;
    }
    public class Functions
    {
        [NoAutomaticTrigger]
        public static void ImportSuosikkiData(
            [Queue("%ADD_MODEL_DATA_QUEUE%")] ICollector<string> addModelDataQueue,
            CloudStorageAccount storageAccount,
            TextWriter log)
        {
            var blobClient = storageAccount.CreateCloudBlobClient();
            var rawUsageDataContainer = blobClient.GetContainerReference(
                ConfigurationManager.AppSettings["USAGE_DATA_CONTAINER"].ToString());
            rawUsageDataContainer.CreateIfNotExists();
            int bytes = 0;
            foreach (var blob in rawUsageDataContainer.ListBlobs())
            {
                // check if we're looking at a file or a folder
                if (blob.Uri.AbsolutePath.Contains(".csv"))
                {
                    log.WriteLine("Start processing blob {0}", blob.Uri.AbsolutePath);
                    string blobName = blob.Uri.AbsolutePath.Replace(rawUsageDataContainer.Uri.AbsolutePath + "/", "");
                    using (var memoryStream = new MemoryStream())
                    {
                        rawUsageDataContainer.GetBlobReference(blobName).DownloadToStream(memoryStream);
                        using (var reader = new StringReader(System.Text.Encoding.UTF8.GetString(memoryStream.ToArray())))
                        {
                            string line = null;
                            var lines = new StringBuilder();
                            while ((line = reader.ReadLine()) != null)
                            {
                                bytes += System.Text.UnicodeEncoding.UTF32.GetByteCount(line);
                                if (bytes > Config.MESSAGE_SIZE)
                                {
                                    bytes = 0;
                                    addModelDataQueue.Add(lines.ToString());
                                    lines = new StringBuilder();
                                }
                                lines.AppendLine(line);
                            }                      
                            if (lines.Length > 0)
                            {
                                addModelDataQueue.Add(lines.ToString());
                            }
                        }
                        // move the blob to the processed folder
                        var processedBlob = rawUsageDataContainer.GetBlockBlobReference(string.Format("processed/{0}", blobName));
                        processedBlob.UploadFromStream(memoryStream);
                        log.WriteLine("Finished processing blob {0}", blob.Uri.AbsolutePath);
                    }
                    // delete the original blob
                    rawUsageDataContainer.GetBlobReference(blobName).DeleteIfExists();
                }
            }
        }
    }
}
