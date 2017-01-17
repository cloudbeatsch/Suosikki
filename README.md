# Suosikki recommendation pipeline
The WebJobs in this repo create the input data to train the Azure ML and the Cognitive Services recommendation engines.
To do so, it takes the podcast usage data (which is exported from the SQL DW) and
enriches this with content data stored in DocumentDB. 

## Deploying the solution
To deploy the solution, simply deploy the `WebSite.json` arm template, which is part of the `Suosikki.WebJobs.Deployment` project.
This can be either done through script (CLI/Powershell) or by using Visual Studio:
1. Open the solution file in Visual Studio
2. Open the `Solution Explorer`
3. Select the `SuosikkiRecommendSuosikki.WebJobs.Deployment` project and right click
4. Select `Deploy`

Deploy the arm template will create the following resources:
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

### Configuring the pipeline
Important WebSite settings and their default values:
* the container where the csv data will be uploaded: 

``` 
    "USAGE_DATA_CONTAINER": "rawusagedata" 
```

* Output table names:

``` 
    "USER_EPISODE_RATING_TABLE": "UserEpisodeRating"
    "USER_SHOW_RATING_TABLE": "UserShowRating" 
    "EPISODE_FEATURES_TABLE": "EpisodeFeatures"
    "SHOW_FEATURES_TABLE": "ShowFeatures"
    "USER_FEATURES_TABLE": "UserFeatures" 
```
* DocumentDB related settings:

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