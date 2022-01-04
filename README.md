# EventGrid Function Triggers to process blob files (.NET function App)

This repo is adapted from https://github.com/Azure-Samples/function-image-upload-resize 


Goals: 
- [x] replicate the `Thumbnail` function to generate image thumbnails on upload of image files
- [x] create an Extractdoc function to extract a stringifie 
- [ ] stretch - update the whole thing to functions v3 or v4

1. Create the required Azure resources

    Using the Azure CLI :
    
    ```bash
    #!/bin/bash

    # Function app and storage account names must be unique.
    newResourceGroup=EventFunctionTestRG
    storageName=utilitystorage$RANDOM
    functionAppName=functionapp-cs-$RANDOM
    region=uksouth

    # Create a resource group.

    az group create --name $newResourceGroup --location $region

    # Create an Azure storage account in the resource group.
    az storage account create \
    --name $storageName \
    --location $region \
    --resource-group $newResourceGroup \
    --sku Standard_LRS

    # Create a serverless function app in the resource group.
    az functionapp create \
    --name $functionAppName \
    --storage-account $storageName \
    --consumption-plan-location $region \
    --resource-group $newResourceGroup \
    --functions-version 2
    ```

2. Configure the App settings to match the local setting used in the local test:

    Get the connection string for the data storage account that will trigger the function. Copy this manually from your local.settings.json file and update the function App configuration blade in the Azure portal. 

    or use the CLI:

    ```
    connstr=$(az storage account show-connection-string -g <resource group containing data storage account> -n <datastorageaccountname> -o tsv)
    ```

    update the function app settings:
    
    ```
    az functionapp config appsettings set --name $functionAppName --resource-group $newResourceGroup --settings  LANDING_ZONE=$connstr THUMBNAIL_CONTAINER_NAME=thumbnails THUMBNAIL_WIDTH=100 

    az functionapp config appsettings list -g $newResourceGroup -n $functionAppName

    ```

3. Deploy the function app

   > Note: using the azure CLI with code pushed to a repo, since using function core tools would currently require a downgrade of the local tooling to v2./

    from the root folder of this repo:
    ```
    az functionapp deployment source config --name $functionAppName --resource-group $newResourceGroup --branch main --manual-integration --repo-url https://github.com/lindacmsheard/trigger-file-transform-with-eventgrid-and-azure-function  

    ```
    for later updates to the code base (explicit sync required due to the --manual-integration option above)
    ```
    az functionapp deployment source sync --name $functionAppName --resource-group $newResourceGroup
    ```

    > Note: if this command errors with a note that it can't find the function app, the resource deployment may not yet have finished. Wait and try again. 

4. Create an Event Grid subscription

    This eventgrid subscription connects events that are raised when a new blob is created to the execution of the function.

    ```
    functionappid=$(az resource list -g $newResourceGroup --query "[?name=='$functionAppName' && kind=='functionapp'].id" -o tsv)
    sourceid=$(az resource list -n <data storage account name> -g <rg of data storage account> --query [].id -o tsv )

    az eventgrid event-subscription create  --name imageresizesub \
                                            --source-resource-id $sourceid \
                                            --included-event-types Microsoft.Storage.BlobCreated \
                                            --subject-begins-with /blobServices/default/containers/images/ \
                                            --subject-ends-with .jpg \
                                            --endpoint-type azurefunction \
                                            --endpoint $functionappid/functions/Thumbnail \
                                            --labels function-thumbnail

                                          
    ```


5. Test the function by uploading an image to the image container in the LANDING_ZONE storage account and observing the thumbnail container.




## Create a new function to process json documents

0. Pre-reqs

Create two containers in the LANDING_ZONE storage account:
- `changefeedtest`: this will be the landing location for the incoming json documents, analogous to the `images` container in the previous example.
- `changefeeddocs`: This wwil be the destination for storing the extracted json string as documents, analogous to the `thumbnails` container in the previous example. 

Add the following lineitem to the `values` object in the  `local.settins.json` file:
```
    "DOC_CONTAINER_NAME": "changefeeddocs",
```


1. Review ./ImageFunctions/Extractdoc.cs

TODO: figure out whether ImageFunctions can just be renamed to BlobEventFunctions, and whether just adding a new CS doc to the project is sufficient for another function to be compiled on deploy

2. Test locally

start the function locally
```
dotnet --version
//-> 6.0.100

cd ImageFunctions

func start --csharp

// ->
Functions:

        Extractdoc: eventGridTrigger

        Thumbnail: eventGridTrigger
```

Authenticate and start up ngrok
```
ngrok authtoken <token from online ngrok account>  # once, will be saved in config file at ~/.ngrok2/ngrok.yml

ngrok http -host-header=localhost 7071
```

Add a storage account event subscription to call the locally running function via ngrok

```
sourceid=$(az resource list -n <data storage account name> -g <rg of data storage account> --query [].id -o tsv )
ngrokendpoint=https://<guid_from_ngrok_output>.ngrok.io

az eventgrid event-subscription create  --name testdocsub \
                                        --source-resource-id $sourceid \
                                        --included-event-types Microsoft.Storage.BlobCreated \
                                        --subject-begins-with /blobServices/default/containers/changefeedtest/ \
                                        --subject-ends-with .json \
                                        --endpoint-type webhook \
                                        --endpoint  $ngrokendpoint/runtime/webhooks/EventGrid?functionName=Extractdoc \
                                        --labels function-extractdoc-localtest
```

Within 5 minutes, navigate to the ngrok logs `localhost:4040` in the browser to inspect the POST request sent to ngrok by the event-subscription create event, and navigate to the validationUrl received in the message to complete the provisioning. 


3. Re-deploy the function app

Push any new code to the `main` branch, and then:
```
az functionapp deployment source sync --name $functionAppName --resource-group $newResourceGroup
```


Update the function app settings:

```
az functionapp config appsettings set -n $functionAppName -g $newResourceGroup --settings "DOC_CONTAINER_NAME=changefeeddocs"
```

4. Add a new eventgrid subscription 
```
   az eventgrid event-subscription create  --name jsonextractdocsub \
                                            --source-resource-id $sourceid \
                                            

```

Optionally, add an advanced filter to limit the trigger to a particular folder:

```
  az eventgrid event-subscription update  --name jsonextractdocsub \
                                            --source-resource-id $sourceid \
                                            --advanced-filter data.url StringContains /changefeeddocs/in/
```

5. Test

To measure the time taken when uploading folders to the landing zone, get the time modified of the first and last file with:

```
container = changefeeddocs
path = <blob_prefix>
az storage blob list --num-results "*" -c $container --prefix $path --account-name lcmgexplorestorage --query "[].properties.creationTime | length(@)"
az storage blob list --num-results "*" -c $container --prefix $path --account-name lcmgexplorestorage --query "[].properties.creationTime | sort(@)[0]"
az storage blob list --num-results "*" -c $container --prefix $path --account-name lcmgexplorestorage --query "[].properties.creationTime | sort(@)[-1:]"

```

This Eventgrid + CSharp function implementation outperforms the BlobTrigger + Python function implementation, primarily because of the single threaded nature of the Azure python function app, which scales out to more workers only under significant load. 

For folders of 50000 - 100000 small files, the limiting factor is the upload speed - from Azure compute, this is around `~3 ms` per file for larger batches, and the function execution completes at the same time as the upload completes. 

This means a folder of 10000 files is processed in around 40 seconds, and a folder of 100000 files in around 5 minutes

Conversely, the python app implementation takes 12 minutes and 1h:45m respectively, i.e on average `~60ms` per file

---

### Original Azure Sample repo readme below


---
page_type: sample
languages:
- csharp
products:
- azure
description: "This sample demonstrates how to respond to an EventGridEvent published by a storage account to resize an image and upload a thumbnail as described in the article Automate resizing uploaded images using Event Grid."
urlFragment: function-image-upload-resize
---

# Image Upload Resize 

This sample demonstrates how to respond to an `EventGridEvent` published by a storage account to resize  an image and upload a thumbnail as described in the article [Automate resizing uploaded images using Event Grid](https://docs.microsoft.com/azure/event-grid/resize-images-on-storage-blob-upload-event?toc=%2Fazure%2Fazure-functions%2Ftoc.json&tabs=net).

## Local Setup

Before running this sample locally, you need to add your connection string to the `AzureWebJobsStorage` value in a file named `local.settings.json` file. This file is excluded from the git repository, so an example file named `local.settings.example.json` is provided.

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "<STORAGE_ACCOUNT_CONNECTION_STRING>",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "THUMBNAIL_CONTAINER_NAME": "thumbnails",
    "THUMBNAIL_WIDTH":  "100",
    "datatype": "binary"
  }
}
```

To use this file, do the following steps:

1. Replace `<STORAGE_ACCOUNT_CONNECTION_STRING>` with your storage account connection string
2. Rename the file from `local.settings.example.json` to `local.settings.json` 

## Version Support

The `master` branch of this repository contains the Functions version 2.x implementation, while the `v1` branch has the Functions 1.x implementation.

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
