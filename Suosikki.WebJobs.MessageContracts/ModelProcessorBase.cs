using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Suosikki.WebJobs.Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Suosikki.WebJobs.Common
{
    public class ModelProcessorCreationMsg
    {
        public string modelType;
        public CloudTableClient tableClient;
        public CloudBlobContainer cogInputDataContainer;
        public TextWriter log;
        public bool queryUserFeatures;
        public bool queryItemFeatures;
    }
    public abstract class ModelProcessorBase
    {
        protected string modelType;
        protected CloudTable userItemRatingTable;
        protected CloudTable itemFeaturesTable;
        protected CloudTable userFeaturesTable;
        protected CloudAppendBlob itemFeaturesBlob;
        protected CloudAppendBlob usageBlob;
        protected TextWriter log;
        protected bool queryUserFeatures;
        protected bool queryItemFeatures;

        public ModelProcessorBase(ModelProcessorCreationMsg creationMsg)
        {
            this.modelType = creationMsg.modelType;
            this.log = creationMsg.log;
            this.queryUserFeatures = creationMsg.queryUserFeatures;
            this.queryItemFeatures = creationMsg.queryItemFeatures;
            var modelName = modelType.ToString();
            userItemRatingTable = creationMsg.tableClient.GetTableReference(modelName + "UserItemRating");
            userItemRatingTable.CreateIfNotExists();
            itemFeaturesTable = creationMsg.tableClient.GetTableReference(modelName + "ItemFeatures");
            itemFeaturesTable.CreateIfNotExists();
            userFeaturesTable = creationMsg.tableClient.GetTableReference(modelName + "UserFeatures");
            userFeaturesTable.CreateIfNotExists();


            itemFeaturesBlob = creationMsg.cogInputDataContainer.GetAppendBlobReference(modelName + "/catalogue/" + Guid.NewGuid() + ".csv");
            itemFeaturesBlob.CreateOrReplace();
            usageBlob = creationMsg.cogInputDataContainer.GetAppendBlobReference(modelName + "/usage/" + Guid.NewGuid() + ".csv");
            usageBlob.CreateOrReplace();
        }
        
        protected bool EntityExistsInTable(CloudTable table, string PartitionKey, string RowKey)
        {
            TableOperation retrieveOperation = TableOperation.Retrieve<TableEntity>(PartitionKey, RowKey);

            // Execute the retrieve operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);
            return retrievedResult.Result != null;
        }

         
        public void ProcessUsageLine<TUsage_Entity>(UsageEntityBase usageEntity) where TUsage_Entity : UsageEntityBase
        {
            string usageStr = string.Format("{0},{1},{2}{3}", usageEntity.UserId, usageEntity.ItemId, usageEntity.DateTime, Environment.NewLine);
            usageBlob.AppendText(usageStr);

            if (queryUserFeatures)
            {
                ProcessUserFeatures<TUsage_Entity>(usageEntity);
            }

            if (queryItemFeatures)
            {
                ProcessItemFeatures<TUsage_Entity>(usageEntity);
            }
            // write the user item rating triple
            userItemRatingTable.Execute(TableOperation.InsertOrReplace(new UserItemRatingEntity()
            {
                PartitionKey = usageEntity.UserId,
                RowKey = usageEntity.ItemId,
                Rating = usageEntity.Rating
            }));
        }


        protected abstract void ProcessItemFeatures<TUsage_Entity>(UsageEntityBase usageEntity) where TUsage_Entity : UsageEntityBase;
        protected abstract void ProcessUserFeatures<TUsage_Entity>(UsageEntityBase usageEntity) where TUsage_Entity : UsageEntityBase;

        protected string GetFeatureListString(List<string> features)
        {
            string featureListStr = "";
            if (features.Count > 0)
            {
                foreach (var feature in features)
                {
                    if (feature != "")
                    {
                        featureListStr = string.Format("{0},{1}=true", featureListStr, feature);
                    }
                }
            }
            else
            {
                featureListStr = ",nofeature=true";
            }
            return featureListStr;
        }

        public SingleUploadMsg GetUploadMessage()
        {
            return new SingleUploadMsg()
            {
                ModelType = modelType,
                UsageBlobName = usageBlob.Name,
                CatalogueBlobName = itemFeaturesBlob.Name
            };
        }
    }
}
