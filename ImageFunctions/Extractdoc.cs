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
using System.Threading.Tasks;

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

        // private static IImageEncoder GetEncoder(string extension)
        // {
        //     IImageEncoder encoder = null;

        //     extension = extension.Replace(".", "");

        //     var isSupported = Regex.IsMatch(extension, "gif|png|jpe?g", RegexOptions.IgnoreCase);

        //     if (isSupported)
        //     {
        //         switch (extension.ToLower())
        //         {
        //             case "png":
        //                 encoder = new PngEncoder();
        //                 break;
        //             case "jpg":
        //                 encoder = new JpegEncoder();
        //                 break;
        //             case "jpeg":
        //                 encoder = new JpegEncoder();
        //                 break;
        //             case "gif":
        //                 encoder = new GifEncoder();
        //                 break;
        //             default:
        //                 break;
        //         }
        //     }

        //     return encoder;
        // }

        [FunctionName("Extractdoc")]
        public static async Task Run(
            [EventGridTrigger]EventGridEvent eventGridEvent,
//            [Blob("{data.url}", FileAccess.Read)] Stream input,
            [Blob("{data.url}", FileAccess.Read, Connection = "LANDING_ZONE")] Stream input,
            ILogger log)
        {
            try
            {
                if (input != null)

                {
                    var createdEvent = ((JObject)eventGridEvent.Data).ToObject<StorageBlobCreatedEventData>();
                    var extension = Path.GetExtension(createdEvent.Url);
                // NEW CODE HERE:

                    var isjson= Regex.IsMatch(extension, "json", RegexOptions.IgnoreCase);

                    if (isjson)
                    {
                        var blobServiceClient = new BlobServiceClient(BLOB_STORAGE_CONNECTION_STRING);
                        var blobContainerClient = blobServiceClient.GetBlobContainerClient(thumbContainerName);
                        var blobName = GetBlobNameFromUrl(createdEvent.Url);

                        log.LogInformation($"Processing: {createdEvent.Url}");

                        using (var output = new MemoryStream())
                        using (JSON<json> changefeeditem = JSON.Load(input))                // this is pseudo code - need to identify types to use and how to read json from stream into object
                        {
                            
                            // Code here to manipulate the input, 
                            // extract the stringified json, 
                            // and push the resutling object into the output stream


                            output.Position = 0;
                            await blobContainerClient.UploadBlobAsync(blobName, output);
                        }
                    }
                    else
                    {
                        log.LogInformation($"Received a non-json input: {createdEvent.Url}");
                    }

                // IMAGE EXAMPLE
                //     var encoder = GetEncoder(extension);

                //     if (encoder != null)
                //     {
                //         var thumbnailWidth = Convert.ToInt32(Environment.GetEnvironmentVariable("THUMBNAIL_WIDTH"));
                //         var thumbContainerName = Environment.GetEnvironmentVariable("THUMBNAIL_CONTAINER_NAME");
                //         var blobServiceClient = new BlobServiceClient(BLOB_STORAGE_CONNECTION_STRING);
                //         var blobContainerClient = blobServiceClient.GetBlobContainerClient(thumbContainerName);
                //         var blobName = GetBlobNameFromUrl(createdEvent.Url);

                //         log.LogInformation($"Processing: {createdEvent.Url}");

                //         using (var output = new MemoryStream())
                //         using (Image<Rgba32> image = Image.Load(input))
                //         {
                //             var divisor = image.Width / thumbnailWidth;
                //             var height = Convert.ToInt32(Math.Round((decimal)(image.Height / divisor)));

                //             image.Mutate(x => x.Resize(thumbnailWidth, height));
                //             image.Save(output, encoder);
                //             output.Position = 0;
                //             await blobContainerClient.UploadBlobAsync(blobName, output);
                //         }
                //     }
                //     else
                //     {
                //         log.LogInformation($"No encoder support for: {createdEvent.Url}");
                //     }

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
