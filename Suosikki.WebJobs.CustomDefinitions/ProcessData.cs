
using Microsoft.Azure.Documents.Client;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;


namespace Suosikki.WebJobs.CustomDefinitions
{
    // There will be only one instance of ProcessData per process
    // All webjobs in this process will leverage the same instance
    public class ProcessData
    {
        private static ProcessData processData = null;
        private static Object lockObj = new Object();
        private DocumentClient docDbClient;

        // ctor is private - so an instance can only be created using ProcessDataFactory()
        private ProcessData()
        {
            // webjobs execute in parallel, so we lock to ensure 
            // that only one instance of processdata gets created
            lock (lockObj)
            {
                if (processData == null)
                {
                    docDbClient = new DocumentClient(
                        new Uri(ConfigurationManager.AppSettings["DOCDB_URI"].ToString()),
                        ConfigurationManager.AppSettings["DOCDB_KEY"].ToString(),
                        new ConnectionPolicy
                        {
                            ConnectionMode = ConnectionMode.Direct,
                            ConnectionProtocol = Protocol.Tcp
                        });
                    processData = this;
                }
            }
        }

        public static ProcessData ProcessDataFactory()
        {
            if (processData == null)
            {
                new ProcessData();
            }
            return processData;
        }

        public DocumentClient GetDocDbClient()
        {
            return docDbClient;
        }

    }
}
