using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
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

namespace Suosikki.WebJobs.CustomDefinitions
{
    internal class DocumentDBModelProcessor<TUsage_Entity> : ModelProcessorBase where TUsage_Entity : UsageEntity
    {
        public DocumentDBModelProcessor(ModelProcessorCreationMsg creationMsg)
           : base (creationMsg)
        {

        }

        protected override void ProcessUserFeatures<TUsage_Entity_Func>(UsageEntityBase usageEntityBase) 
        {
            TUsage_Entity usageEntity = (TUsage_Entity)usageEntityBase;
            // only query for the user features if we don't already have it
            if (!EntityExistsInTable(userFeaturesTable, usageEntity.UserId, usageEntity.UserId))
            {
                var u = GetUserFeatures(usageEntity.UserId);

                // write the user features
                userFeaturesTable.Execute(TableOperation.InsertOrReplace(u));
                        log.WriteLine("Added user features for {0}", usageEntity.UserId);
            }
        }

        protected override void ProcessItemFeatures<TUsage_Entity_Func>(UsageEntityBase usageEntityBase)
        {
            TUsage_Entity usageEntity = (TUsage_Entity) usageEntityBase;
            // only query for the item features if we don't already have it
            if (!EntityExistsInTable(itemFeaturesTable, usageEntity.ItemId, usageEntity.PartitionKey))
            {
                var categories = new List<string>();
                var i = GetItemFeatures(usageEntity.ItemId, usageEntity.PartitionKey, categories);
                // with features - <Item Id>,<Item Name>,<Item Category>,[<Description>],<Features list> (name=feature value)
                itemFeaturesBlob.AppendText(string.Format("{0},{1},{2}, {3}{4}", i.RowKey, i.Properties["Title"].StringValue, i.Properties["Author"].StringValue, GetFeatureListString(categories), Environment.NewLine));
                // write the items feature
                itemFeaturesTable.Execute(TableOperation.InsertOrReplace(i));
                log.WriteLine("Added item features for {0}", usageEntity.ItemId);
            }
        }
        private dynamic GetDocument(string id, string partitionKey)
        {
            FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1 };
            var t = ProcessData.ProcessDataFactory().GetDocDbClient().ReadDocumentAsync(
                  UriFactory.CreateDocumentUri(
                      ConfigurationManager.AppSettings["DOCDB_DATABASE"].ToString(),
                      ConfigurationManager.AppSettings["DOCDB_COLLECTION"].ToString(),
                      id),
                  new RequestOptions { PartitionKey = new PartitionKey(partitionKey) });
            try
            {
                t.Wait();
                return t.Result.Resource;
            }
            catch (Exception)
            {
                // We just return a dummy Document
                return new {
                    birthday = "01.01.2000",
                    gender = "feMale",
                    Country = "Switzerland",
                    language = "en",
                    title = "DocTitle",
                    author = "DocAuthor",
                    categories = new[] { "news", "finance", "sport"}
                };
            }
        }
        private UserFeaturesEntity GetUserFeatures(string userId)
        {
            dynamic userDoc = GetDocument(userId, userId);
            return userDoc != null ? new UserFeaturesEntity()
            {
                PartitionKey = userId,
                RowKey = userId,
                Birthday = userDoc.birthday != null ? userDoc.birthday : Config.NA_STR,
                Gender = userDoc.gender != null ? userDoc.gender : Config.NA_STR,
                Country = userDoc.country != null ? userDoc.country : Config.NA_STR
            } : new UserFeaturesEntity()
            {
                PartitionKey = Config.NA_STR,
                RowKey = userId,
                Birthday = Config.NA_STR,
                Gender = Config.NA_STR,
                Country = Config.NA_STR
            };
        }

        private DynamicTableEntity GetItemFeatures(string itemId, string partitionKey, List<string> categories)
        {
            dynamic itemDoc = GetDocument(partitionKey, partitionKey);
            var itemEntity = new DynamicTableEntity()
            {
                PartitionKey = partitionKey,
                RowKey = itemId 
            };
            Regex propertyRgx = new Regex("[^a-zA-Z-]");
            if (itemDoc != null)
            {
                itemEntity.Properties.Add("Language", new EntityProperty((string)(itemDoc.language != null ? itemDoc.language : Config.NA_STR)));
                itemEntity.Properties.Add("Title", new EntityProperty((string)(itemDoc.title != null ? propertyRgx.Replace(itemDoc.title.ToLower(), "") : Config.NA_STR)));
                itemEntity.Properties.Add("Author", new EntityProperty((string)itemDoc.author != null ? propertyRgx.Replace(itemDoc.author.ToLower(), "") : Config.NA_STR));
                foreach (var category in itemDoc.categories)
                {
                    try
                    {
                        string propertyName = propertyRgx.Replace(category.ToString().ToLower(), "");
                        categories.Add(propertyName);
                        itemEntity.Properties.Add(propertyName, new EntityProperty(true));
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(string.Format("item with {0} contains invalid category {1}", itemId, category), ex);
                    }
                }
            }
            return itemEntity;
        }
    }
}
