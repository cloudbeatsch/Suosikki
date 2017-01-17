using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Suosikki.WebJobs.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suosikki.WebJobs.Common
{
    public class ModelProcessorCollection<TUsage_Entity> : IModelProcessorCollection where TUsage_Entity : UsageEntityBase, new()
    {
        private CloudTableClient tableClient;
        private CloudBlobContainer cogInputDataContainer;
        private TextWriter log;
        private bool queryUserFeatures;
        private bool queryItemFeatures;
        private Dictionary<string, ModelProcessorBase> modelProcessors;


        public ModelProcessorCollection(CloudTableClient tableClient, CloudBlobContainer cogInputDataContainer, TextWriter log, bool queryUserFeatures, bool queryItemFeatures)
        {
            this.tableClient = tableClient;
            this.cogInputDataContainer = cogInputDataContainer;
            this.log = log;
            this.queryUserFeatures = queryUserFeatures;
            this.queryItemFeatures = queryItemFeatures;
            this.modelProcessors = new Dictionary<string, ModelProcessorBase>();
        }

        public void ProcessUsageLine(string line)
        {
            var usageEntity = new TUsage_Entity();
            usageEntity.ParsLine(line);
            if (usageEntity.IsValid)
            {
                GetModelProcessor(usageEntity).ProcessUsageLine<TUsage_Entity>(usageEntity);
            }
        }

        private ModelProcessorBase GetModelProcessor(TUsage_Entity usageEntity)
        {
            if (!modelProcessors.ContainsKey(usageEntity.ModelType))
            {
                modelProcessors[usageEntity.ModelType] = usageEntity.CreateModelProcessor<TUsage_Entity>(
                    new ModelProcessorCreationMsg()
                    {
                        modelType = usageEntity.ModelType,
                        tableClient = tableClient,
                        cogInputDataContainer = cogInputDataContainer,
                        log = log,
                        queryUserFeatures = queryUserFeatures,
                        queryItemFeatures = queryItemFeatures
                    });
            }
            return modelProcessors[usageEntity.ModelType];
        }

        public void CreateUploadMessages(ICollector<SingleUploadMsg> uploadQueue)
        {
            foreach (var model in modelProcessors.Values)
            {
                uploadQueue.Add(model.GetUploadMessage());
            }
        }
    }
}
