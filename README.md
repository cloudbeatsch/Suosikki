# Suosikki recommendation pipeline
Suosikki is an automated pipeline which takes incremental raw usage data and creates the datasets needed to train the [Matchbox Recommender](https://msdn.microsoft.com/en-us/library/azure/dn905987.aspx) and the [Recommendations API](https://www.microsoft.com/cognitive-services/en-us/recommendations-api) of [Cognitive Services](https://www.microsoft.com/cognitive-services/en-us/). 
For the latter, we also upload the data and re-train the model. For Matchbox, re-training and re-deploying the model remains a manual task.
The reason why we created a pipeline that leverages both approaches is two folded:
- The two platforms are complementary
- The ultimate goal is to figure out which service will yield the better results for our partner (determined by A/B testing)

## Overview
With this in mind we created a pipeline that splits the raw usage data into smaller batches for parallel processing, then creates the datasets for the two algorithms. 
In the case of Cognitive Services, we also upload the catalogue and usage data before we re-train the model:

![Overview](/images/Overview_Suosikki_Pipeline.JPG?raw=true "Overview")


## Deploying the solution
To deploy the solution, simply push the button

[![Deploy to Azure](http://azuredeploy.net/deploybutton.png)](https://azuredeploy.net/)

The deployment will create the following resources:
* Azure Resource Group
* Azure AppServicePlan
* Azure WebSite for hosting the WebJobs
* Azure storage account

## How the pipeline works
The first pipeline step is the `Suosikki.WebJobs.UsageLoader` WebJob. This job will be triggered according to its Cron expression. 
This expression is defined in its `settings.job`. The default schedule is `"schedule": "0 0 22 * * *`, which means that the loader job runs daily at 10pm.
The job checks for a new usage file in the container specified in the `USAGE_DATA_CONTAINER` application setting. 
The default container is `rawusagedata`. To start the pipeline, simply upload a csv file (comma delimited!) to this blob container. 
The format of the uploaded csv file should be in the following format:

```
userId, itemId, timestamp, rating, type (V for Video, B for Book, M for Music), partitionKey (showId)
```
e.g.

```
87df7422-0d7e-478f-a48e-25915b4d4c92,972a994d-a86f-47ce-8e88-5e955d47f7ee,2016/06/01T01:00:00,8,V,972a994d-a86f-47ce-8e88-5e955d47f7ee
```
The second job in the pipeline is `Suosikki.WebJobs.UsageLoader`. This WebJob splits the input csv files into smaller sized chunks and triggers the `Suosikki.WebJobs.FeatureCreator` WebJob. 
The feature creator creates the tables which are used to train the Azure ML models. In addition, it also creates intermediate upload files for the Cognitive Services API.

The last two steps in the pipeline are `Suosikki.WebJobs.PrepareCognitiveServicesUpload` and `Suosikki.WebJobs.CognitiveServices`: 
The `Suosikki.WebJobs.PrepareCognitiveServicesUpload` WebJob aggregates the intermediate files into upload files of 100MB. 
Per default, the job runs every day at 8am (this can be easily changed by defining a different Cron expression). 
The job also triggers the `Suosikki.WebJobs.CognitiveServices` WebJob, which uploads the files to cognitive services. Once all files are uploaded, it will trigger the build of the model.
It stores the upload statistics and the build results into the stats folder of the `coginputdata` container.

## Customizing the pipeline
All customization takes place in the `Suosikki.WebJobs.CustomDefinitions` library. Here a quick step by step guide to adapt the pipline to your own needs:
1. ***Define your models*** - the [ModelTypeEnum](https://github.com/cloudbeatsch/Suosikki/blob/master/Suosikki.WebJobs.CustomDefinitions/Definitions.cs#L9) defines what models are created. For instance, the below definition triggers for each of the three models `Music`, `Books` and `Videos` the creation of an item and user table (Matchbox) as well as the upload of catalogue and usage data (Cognitive Services):  

```public enum ModelTypeEnum { Music, Books, Videos };```

2. ***Implement the UsageEntity*** - The [UsageEntity](https://github.com/cloudbeatsch/Suosikki/blob/master/Suosikki.WebJobs.CustomDefinitions/UsageEntity.cs) derives from [UsageEntityBase](https://github.com/cloudbeatsch/Suosikki/blob/master/Suosikki.WebJobs.Common/UsageEntityBase.cs) and is responsible to handle the parsing of the input usage data. It basically extracts all relevant information from the input line and stores it as properties. Beside `ParseLine` it also implements the `CreateModelProcessor` method. This method returns an instance of a ModelProcessor which can deal with the current `ModelType`. The ModelProcessor is responsible to enrich the usage data with content and meta data coming from additional datasources. Different models might require different ModelProcessors.

3. ***Implement the ModelProcessors*** - ModelProcessors create the feature vectors and write them to table storage (Matchbox) and blob storage (Cognitive Services). An example of a ModelProcessor that gets its content data from DocumentDB can be found [here](https://github.com/cloudbeatsch/Suosikki/blob/master/Suosikki.WebJobs.CustomDefinitions/DocumentDBModelProcessor.cs). ModelProcessors derive from [ModelProcessorBase](https://github.com/cloudbeatsch/Suosikki/blob/master/Suosikki.WebJobs.Common/ModelProcessorBase.cs) and are required to implement the following two methods:
```
   protected abstract void ProcessItemFeatures<TUsage_Entity>(UsageEntityBase usageEntity) where TUsage_Entity : UsageEntityBase; 
   protected abstract void ProcessUserFeatures<TUsage_Entity>(UsageEntityBase usageEntity) where TUsage_Entity : UsageEntityBase; 
``` 
4. ***Wire-up the customization*** - Your `UsageEntity` knows how to pars your input data and your ModelProcessors know how to create the feature vectors. Finally we just need to wire them up with the `FeatureCreature` WebJob. To do so, we simply specify our pipelines `UsageEntity` when instantiating a new `ModelProcessorCollection` (see full statement [here](https://github.com/cloudbeatsch/Suosikki/blob/master/jobs/continuous/Suosikki.WebJobs.FeatureCreator/Functions.cs#L37))
```
   IModelProcessorCollection modelProcessors = new ModelProcessorCollection<UsageEntity>(...);
```

## Configuring the pipeline
Important WebSite settings and their default values:
* the container where the csv data will be uploaded: 

``` 
    "USAGE_DATA_CONTAINER": "rawusagedata" 
```

* DocumentDB related settings (optional - depending on model processors, other configurations might be needed (e.g, SQL connection strings)) :

``` 
    "DOCDB_URI": "YOUR-DOCUMENTDB-URI"
    "DOCDB_KEY": "YOUR-DOCUMENTDB_KEY"
    "DOCDB_DATABASE": "Content"
    "DOCDB_COLLECTION": "Content"
```
* The API key for the cognitive services:
```
    "COG_SERVICES_API_KEY": "YOUR-COG-SERVICES-API-KEY"
```
* The amount of parallel instances that will be used. This can be used to optimize IOPS of Azure storage and DocumentDB. 
(Note: since there are two WebSite instances, the actual number of parallel instances needs to be multiplied by 2):
```
    "QUEUE_BATCH_SIZE" :  "8"
```
## Train and publish the Azure Machine Learning Models in ML Studio
### Training experiment for English and Swedish recommendations
![The training experiment](/images/maml_training_experiment.JPG?raw=true "The training experiment")
### Predictive experiment for English and Swedish recommendations
![The predictive experiment](/images/maml_predictive_experiment.JPG?raw=true "The predictive experiment")