// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}

// Learn how to locally debug an Event Grid-triggered function:
//    https://aka.ms/AA30pjh

// Use for local testing:
//   https://{ID}.ngrok.io/runtime/webhooks/EventGrid?functionName=Extractdoc


// fixed issues with the update the v2 of the tutorial with reference to https://stackoverflow.com/a/53314953

using Azure.Storage.Blobs;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
// using SixLabors.ImageSharp;
// using SixLabors.ImageSharp.Formats;
// using SixLabors.ImageSharp.Formats.Gif;
// using SixLabors.ImageSharp.Formats.Jpeg;
// using SixLabors.ImageSharp.Formats.Png;
// using SixLabors.ImageSharp.PixelFormats;
// using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Text.RegularExpressions;
//using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ImageFunctions
{
    public static class Extractdoc
    {
        //private static readonly string BLOB_STORAGE_CONNECTION_STRING = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        private static readonly string BLOB_STORAGE_CONNECTION_STRING = Environment.GetEnvironmentVariable("LANDING_ZONE");

        private static string GetBlobNameFromUrl(string bloblUrl)
        {
            var uri = new Uri(bloblUrl);
            var blobClient = new BlobClient(uri);
                return blobClient.Name;
        }

        public class EventDoc
        {
            public string metadata { get; set; }
            public string @namespace { get; set; }
            public string fullDocument { get; set; }
        }
            
        public class NamespaceInfo
        {
            public string collectionName { get; set; }
        }

        public class ExtractedDoc
        {
            public string foo { get; set; }
            public string bar { get; set; }
        }


        [FunctionName("Extractdoc")]
        public static async Task Run(
            [EventGridTrigger]EventGridEvent eventGridEvent,
            [Blob("{data.url}", FileAccess.Read, Connection = "LANDING_ZONE")] Stream input,
            ILogger log)
        {
            try
            {
                log.LogInformation(eventGridEvent.Data.ToString());
                
                if (input != null & input.CanRead)

                {
                    var createdEvent = ((JObject)eventGridEvent.Data).ToObject<StorageBlobCreatedEventData>();
                    var extension = Path.GetExtension(createdEvent.Url);

                    var isjson= Regex.IsMatch(extension, "json", RegexOptions.IgnoreCase);

                    if (isjson)
                    {
                        var blobServiceClient = new BlobServiceClient(BLOB_STORAGE_CONNECTION_STRING);
                        var docContainerName = Environment.GetEnvironmentVariable("DOC_CONTAINER_NAME");
                        var blobContainerClient = blobServiceClient.GetBlobContainerClient(docContainerName);
                        var blobName = GetBlobNameFromUrl(createdEvent.Url);

                        log.LogInformation($"Processing: {createdEvent.Url}");

                        string jsonstring = null;
                        using (var output = new MemoryStream())
                        using (StreamReader reader = new StreamReader(input)) 
                        using (StreamWriter writer = new StreamWriter(output))                       
                        {
                            
                            jsonstring = reader.ReadToEnd();
                            log.LogInformation($"Loaded Blob Content: {jsonstring}");

                            EventDoc inputdoc = JsonConvert.DeserializeObject<EventDoc>(jsonstring);

                            log.LogInformation($"Contained FullDocument :{inputdoc.fullDocument}");

                            NamespaceInfo info = JsonConvert.DeserializeObject<NamespaceInfo>(inputdoc.@namespace);

                            log.LogInformation($"Collection: {info.collectionName}");

                            ExtractedDoc doc = JsonConvert.DeserializeObject<ExtractedDoc>(inputdoc.fullDocument); 


                            var jsondoc = JsonConvert.SerializeObject(doc, Formatting.Indented);
                            log.LogInformation(jsondoc);
                            
                            writer.Write(jsondoc);
                            writer.Flush();
                            output.Position = 0;
                            //output.Write(outputbytes, 0, outputbytes.Length);
                            await blobContainerClient.UploadBlobAsync($"{info.collectionName}/{blobName}", output);
                            
                        }
                    }
                    else
                    {
                        log.LogInformation($"Received a non-json input: {createdEvent.Url}");
                    }

                }
                else
                {
                    log.LogInformation("Input was null.");
                }
            }
            catch (Exception ex)
            {
                log.LogInformation(ex.Message);
                throw;
            }
        }
    }

}
