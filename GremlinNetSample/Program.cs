﻿using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Gremlin.Net.Driver;
using Gremlin.Net.Driver.Exceptions;
using Gremlin.Net.Structure.IO.GraphSON;
using Newtonsoft.Json;
using System.Configuration;

namespace GremlinNetSample
{
    /// <summary>
    /// Sample program that shows how to get started with the Graph (Gremlin) APIs for Azure Cosmos DB using the open-source connector Gremlin.Net
    /// </summary>
    class Program
    {
        // Azure Cosmos DB Configuration variables
        // Replace the values in these variables to your own.
        // <configureConnectivity>
        private static readonly string Host = ConfigurationManager.AppSettings["Host"];
        private static readonly int Port = 443;

        private static readonly string PrimaryKey = ConfigurationManager.AppSettings["PrimaryKey"];
        private static readonly string Database = ConfigurationManager.AppSettings["Database"];
        private static readonly string Container = ConfigurationManager.AppSettings["Container"];

        // </configureConnectivity>

        // Gremlin queries that will be executed.
        // <defineQueries>
        private static Dictionary<string, string> gremlinQueries = new Dictionary<string, string>
        {
            { "Cleanup",        "g.V().drop()" },
            { "AddVertex 1",    "g.addV('person').property('id', 'thomas').property('firstName', 'Thomas').property('age', 44).property('partitionKey', 'pk')" },
            { "AddVertex 2",    "g.addV('person').property('id', 'mary').property('firstName', 'Mary').property('lastName', 'Andersen').property('age', 39).property('partitionKey', 'pk')" },
            { "AddVertex 3",    "g.addV('person').property('id', 'ben').property('firstName', 'Ben').property('lastName', 'Miller').property('partitionKey', 'pk')" },
            { "AddVertex 4",    "g.addV('person').property('id', 'robin').property('firstName', 'Robin').property('lastName', 'Wakefield').property('partitionKey', 'pk')" },
            { "AddEdge 1",      "g.V('thomas').addE('knows').to(g.V('mary'))" },
            { "AddEdge 2",      "g.V('thomas').addE('knows').to(g.V('ben'))" },
            { "AddEdge 3",      "g.V('ben').addE('knows').to(g.V('robin'))" },
            { "UpdateVertex",   "g.V('thomas').property('age', 44)" },
            //{ "CountVertices",  "g.V().count()" },
            //{ "Filter Range",   "g.V().hasLabel('person').has('age', gt(40))" },
            //{ "Project",        "g.V().hasLabel('person').values('firstName')" },
            //{ "Sort",           "g.V().hasLabel('person').order().by('firstName', decr)" },
            //{ "Traverse",       "g.V('thomas').out('knows').hasLabel('person')" },
            //{ "Traverse 2x",    "g.V('thomas').out('knows').hasLabel('person').out('knows').hasLabel('person')" },
            //{ "Loop",           "g.V('thomas').repeat(out()).until(has('id', 'robin')).path()" },
            //{ "DropEdge",       "g.V('thomas').outE('knows').where(inV().has('id', 'mary')).drop()" },
            //{ "CountEdges",     "g.E().count()" },
            //{ "DropVertex",     "g.V('thomas').drop()" },
        };

        // </defineQueries>

        // Starts a console application that executes every Gremlin query in the gremlinQueries dictionary. 
        [Obsolete]
        static void Main(string[] args)
        {
            // <defineClientandServerObjects>
            string containerLink = "/dbs/" + Database + "/colls/" + Container;
            Console.WriteLine($"Connecting to: host: {Host}, port: {Port}, container: {containerLink}");

            var gremlinServer = new GremlinServer(
                hostname: Host,
                port: Port,
                enableSsl: true,
                username: containerLink,
                password: PrimaryKey
            );

            using (var gremlinClient = new GremlinClient(
                gremlinServer: gremlinServer,
                graphSONReader: new GraphSON2Reader(),
                graphSONWriter: new GraphSON2Writer(),
                mimeType: GremlinClient.GraphSON2MimeType
            ))
            {
                // </defineClientandServerObjects>

                // <executeQueries>
                foreach (var query in gremlinQueries)
                {
                    Console.WriteLine(String.Format("Running this query: {0}: {1}", query.Key, query.Value));

                    // Create async task to execute the Gremlin query.
                    var resultSet = SubmitRequest(gremlinClient, query).Result;
                    if (resultSet.Count > 0)
                    {
                        Console.WriteLine("\tResult:");
                        foreach (var result in resultSet)
                        {
                            // The vertex results are formed as Dictionaries with a nested dictionary for their properties
                            string output = JsonConvert.SerializeObject(result);
                            Console.WriteLine($"\t{output}");
                        }
                        Console.WriteLine();
                    }

                    // Print the status attributes for the result set.
                    // This includes the following:
                    //  x-ms-status-code            : This is the sub-status code which is specific to Cosmos DB.
                    //  x-ms-total-request-charge   : The total request units charged for processing a request.
                    //  x-ms-total-server-time-ms   : The total time executing processing the request on the server.
                    PrintStatusAttributes(resultSet.StatusAttributes);
                    Console.WriteLine();
                }
                // </executeQueries>
            }

            // Exit program
            Console.WriteLine("Done. Press any key to exit...");
            Console.ReadLine();
        }

        private static Task<ResultSet<dynamic>> SubmitRequest(GremlinClient gremlinClient, KeyValuePair<string, string> query)
        {
            try
            {
                return gremlinClient.SubmitAsync<dynamic>(query.Value);
            }
            catch (ResponseException e)
            {
                Console.WriteLine("\tRequest Error!");

                // Print the Gremlin status code.
                Console.WriteLine($"\tStatusCode: {e.StatusCode}");

                // On error, ResponseException.StatusAttributes will include the common StatusAttributes for successful requests, as well as
                // additional attributes for retry handling and diagnostics.
                // These include:
                //  x-ms-retry-after-ms         : The number of milliseconds to wait to retry the operation after an initial operation was throttled. This will be populated when
                //                              : attribute 'x-ms-status-code' returns 429.
                //  x-ms-activity-id            : Represents a unique identifier for the operation. Commonly used for troubleshooting purposes.
                PrintStatusAttributes(e.StatusAttributes);
                Console.WriteLine($"\t[\"x-ms-retry-after-ms\"] : { GetValueAsString(e.StatusAttributes, "x-ms-retry-after-ms")}");
                Console.WriteLine($"\t[\"x-ms-activity-id\"] : { GetValueAsString(e.StatusAttributes, "x-ms-activity-id")}");

                throw;
            }
        }

        private static void PrintStatusAttributes(IReadOnlyDictionary<string, object> attributes)
        {
            Console.WriteLine($"\tStatusAttributes:");
            Console.WriteLine($"\t[\"x-ms-status-code\"] : { GetValueAsString(attributes, "x-ms-status-code")}");
            Console.WriteLine($"\t[\"x-ms-total-server-time-ms\"] : { GetValueAsString(attributes, "x-ms-total-server-time-ms")}");
            Console.WriteLine($"\t[\"x-ms-total-request-charge\"] : { GetValueAsString(attributes, "x-ms-total-request-charge")}");
        }

        public static string GetValueAsString(IReadOnlyDictionary<string, object> dictionary, string key)
        {
            return JsonConvert.SerializeObject(GetValueOrDefault(dictionary, key));
        }

        public static object GetValueOrDefault(IReadOnlyDictionary<string, object> dictionary, string key)
        {
            if (dictionary.ContainsKey(key))
            {
                return dictionary[key];
            }

            return null;
        }
    }
}
