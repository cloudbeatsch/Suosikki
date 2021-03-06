﻿using Suosikki.WebJobs.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suosikki.WebJobs.CustomDefinitions
{
    public class UsageEntity : UsageEntityBase
    {
        // The format of a usage line is as following:
        //    userId,itemId,datetime(2016/10/01T12:00:03),rating,modelType(E for Episode or S for Session),partitionkey(showId)\r\n
        // these lines are seperated by carrage return and a new line: /r/n
        // Important: the IsValid property needs to be set to true after succesfully parsing the usage line  
        //
        public override void ParsLine(string line)
        {
            // parsing the usage line by splitting it by the defined seperator
            var split = line.Split(Config.SEPERATOR);
            IsValid = false;
            if (split.Length >= 6)
            {
                UserId = split[0];
                ItemId = split[1];
                DateTime = split[2];
                Rating = int.Parse(split[3]);
                switch (split[4])
                {
                    case "B":
                        ModelType = ModelTypeEnum.Books.ToString();
                        IsValid = true;
                        break;
                    case "M":
                        ModelType = ModelTypeEnum.Music.ToString();
                        IsValid = true;
                        break;
                    case "V":
                        ModelType = ModelTypeEnum.Videos.ToString();
                        IsValid = true;
                        break;
                    default:
                        break;
                }
                PartitionKey = split[5];
            }
        }


        public override ModelProcessorBase CreateModelProcessor<TUsage_Entity>(ModelProcessorCreationMsg creationMsg)
        {
            // In othe current scenario, all model types are processed by the same ModelProcesssor: DocumentDBModelProcessor. That's why
            // we just return a new instance of DocumentDBModelProcessor whitout even looking at ModelType. If different models 
            // require different processors, the CreateModelProcessor method might look like this:
            //
            //    if (ModelType == ModelTypeEnum.Books.ToString())
            //       return new BooksModelProcessor<UsageEntity>(creationMsg);
            //    else if (ModelType == ModelTypeEnum.Music.ToString())
            //       return new MusicModelProcessor<UsageEntity>(creationMsg);

            return new DocumentDBModelProcessor<UsageEntity>(creationMsg);
        }

        public string PartitionKey { get; set; }
    }
}
