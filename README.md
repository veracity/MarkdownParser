[![Deploy to Azure](http://azuredeploy.net/deploybutton.png)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FVeracity%2FMarkdownParser%2Fmaster%2Fazuredeploy.json)   <-- Most awesome Azure feature ever!

## Overview
This repo contains the code for Markdown Parser project used to parse .md files to HTML code together with additional metadata like headers tree structure for automatic table of contents generation.
MarkdownParserFunction is Azure Fuction, which waits for event from GitHub (Webhook) with incoming commit, filters files in commit against .md extension.
If any files found, MarkdownParser produces JSON fo reach, containing parsed HTML and metadata.
JSON files are at the end put into blob for further operations.

### NuGet Packages used:
+ [Markdig](https://github.com/lunet-io/markdig)
+ [Octokit](https://github.com/octokit/octokit.net)

### Useful articles:
JSON schema from GitHub Webhook:

https://developer.github.com/v3/activity/events/types/#pushevent 

Azure Functions documentation:

https://docs.microsoft.com/en-us/azure/azure-functions/

Azure Functions Imperative bindings reference:

https://docs.microsoft.com/en-us/azure/azure-functions/functions-reference-csharp#imperative-bindings




# Markdown parser tool
When preparing documentation, we base on GitHub and markdown files (.md) where we put all documentation data. With that approach, we can easily maintain the data and have full control over the workflow. On the other hand, it’s not convenient for external users to go through repository when looking at documentation.
That's why there is a tool which can easily translate .md format to HTML syntax consumed by web browser. 
Thanks to this translation and extraction of additional metadata we can provide user friendly and intuitive documentation which is easy to read and navigate.
The following code description applies to the .NET implementation as it is the base platform used for that tool.

## Implementation and usage
Markdown parser tool is designed as a fully automatic module that is executed every time Documentation repository on GitHub is updated. So, there is no need for user input other than adding or updating documentation.
To achieve that goal, we need to setup and deploy a service that will be able to track repository changes and execute appropriately providing a piece of data which can be easily interpreted by web browser at the end.
Instead of setting up whole service with all infrastructure and management concerns we decided to use completely serverless approach called Azure Functions. It’s a solution for running pieces of code in the cloud where we pay only for the time the code runs and trust Azure to scale as needed.
Source code is available [here](https://github.com/veracity/MarkdownParser).

### Markdown parser Azure Function
Azure Functions among many great functionalities provides templates to get started with key scenarios. One of scenarios is called GitHub webhook.
This template executes our "piece of code" every time event occur in GitHub repositories. Its perfect scenario for us. We wait with our Azure Function until a user makes a change in repository, then GitHub sends an event with data about the change and thanks to webhook Azure executes our Function. And we pay only for that execution, not for idle time between repository updates.
More details about Azure Functions and other templates can be found [here](https://docs.microsoft.com/en-us/azure/azure-functions/functions-overview)


Every Function contains a method called Run. This is an entry point executed by trigger from GitHub webhook:
```csharp
public static async Task<HttpResponseMessage> Run([HttpTrigger(WebHookType = "github")] HttpRequestMessage req,
    Binder binder, TraceWriter log)
{
	log.Info("C# HTTP trigger function processed a request.");
	dynamic data = await req.Content.ReadAsAsync<object>();
	var result = await ProcessCommitAsync(data, binder, log);
	return result
    ? req.CreateResponse(HttpStatusCode.OK)
    : req.CreateErrorResponse(HttpStatusCode.NoContent, "error.");
}
```

Inside the method we can distinguish three parts. 
- Read input request - handled by Azure Function, asynchronous reading of data
- Process request - extract needed data from request, process it in Markdown parser
- Return HTTP response based on status from Markdown parser

### Preparing request for parser

To read input request properly we need to know data structure of the request. As the data comes from GitHub, there we can find how sample webhook payload looks like.
Here is [link](https://developer.github.com/v3/activity/events/types/#pushevent) to GitHub documentation and below small description of key points.

When listening for push event, we expect to get information about current commit. Especially we are interested in what files are added/updated/deleted. If any of them will be .md file, we need to process it.

Sample payload for push event:
```json
{
  "head_commit": {
    "id": "0d1a26e67d8f5eaf1f6ba5c57fc3c7d91ac0fd1c",
  },
  "repository": {
    "id": 35129377,
    "default_branch": "master"
  }
}
```

Processing method in our Azure Function:

```csharp
private static async Task<bool> ProcessCommitAsync(dynamic data, Binder binder, TraceWriter log)
{
    var repositoryId = (long) data.repository.id;
    var branch = (string) data.repository.default_branch;
    var commitId = (string) data.head_commit.id;
    var mdFiles = await GetAllMdFilesTask("MarkdownParser", repositoryId, branch, commitId, log);
    var jsonFiles = PrepareJsonData(mdFiles, log);
    return await Utilities.WriteJsonFilesToFileShareTask(jsonFiles,
        Utilities.GetEnvironmentVariable("AzureWebJobsStorage"),
        Utilities.GetEnvironmentVariable("OutputFileShareName"), log);
}
```

As you can see we expect data like repository id and branch name for accessing correct place in Git repository. Also, we expect commit id.
All of this are needed to get .md files included in commit that triggered this webhook event.

We are going to write output Jsons to File Share. We need to have proper access credentials - in this case data connection string and we define File Share name.
This data is saved in Azure Function settings in Azure Portal so we use helper method to get values.
Utilities class is a helper class used also in Console App described later.

### Ask GitHub API for changes in commit

Having above data we are ready to ask GitHub API for data that we are interested in. To achieve that we use a wrapper called [Octokit](https://github.com/octokit/octokit.net) available as NuGet package.

```csharp
public static async Task<List<Tuple<string, string>>> GetAllMdFilesTask(string appName, long repositoryId,
    string branchName, string commitId, TraceWriter log)
{
	.
	.
	var github = new Octokit.GitHubClient(new Octokit.ProductHeaderValue(appName));
	var commit = await github.Repository.Commit.Get(repositoryId, commitId);
	foreach (var file in commit.Files)
	{
	 // if file is md, read its content and put in result collection.
	}
}
```
You can see that as return object we expect collection of Tuples where Item1 in tuple is md file name and Item2 is md file content.

appName in this case is "MarkdownParser" which is name of our repository.

The code inside foreach loop checks if file has status set to "modified" or "added", in that case reads the content of that file with:
```csharp
var contents =
    await github.Repository.Content.GetAllContentsByRef(repositoryId, file.Filename,
        branchName);
```

And adds it to return collection. If file has status other than "modified" or "added" its considered as deleted. So, there is no way to read its content.
But we need to track those changes also, so we put entry in return collection as well. Just with empty string as content.

With data prepared like this we can execute core code of Markdown parser.

### Markdown parser core

In Markdown parser code, we are interested only in md file content. Input collection of Tuples in method PrepareJsonData is used only for creating similar output.
With a difference that output Tuple Item1 is name of produced json file and Item2 is json content.
When input tuple item2 is empty (because file was deleted), output tuple Item2 will also we empty but still there will be entry in output collection.
```csharp
public static List<Tuple<string, string>> PrepareJsonData(List<Tuple<string, string>> mdFiles,
    TraceWriter log)
{
	.
	.
	foreach (var mdFile in mdFiles)
	{
		var mdParser = new MarkdownParser.MarkdownParser();
		var jsonText = mdParser.CreateJson(mdFile.Item2);
		jsonList.Add(new Tuple<string, string>($"{mdFile.Item1}.json", jsonText));
	}
	.
	.
}
```

As an immediate result for JSON serialization we expect object of type MarkdownData.

```csharp
public class MarkdownData
{
    public string HtmlString { get; set; }
    public List<Header> HeaderData { get; set; }
    public Dictionary<string, string> Metadata {g et;set; }
}
```
HtmlString is a properly formatted HTML web page created based on .md input.
HeaderData is a metadata extracted from .md input defining tree structure of headers. Its used for menu creation.
MetaData is information extracted from frontmatter or md file containg data like document title or author.

For pure HTML generation [Markdig](https://github.com/lunet-io/markdig) library is used. As one of middle steps it produces nice object model which is used by us for Headers tree structure generation.

The Markdig library is used as follows:
```csharp
using (var writer = new StringWriter())
{
    var builder = new MarkdownPipelineBuilder();
    builder.UseAutoIdentifiers();
    builder.UsePipeTables();
    builder.UseFigures();
    builder.UseCustomCodeBlockExtension();
    document = Markdown.ToHtml(mdFileContent, writer, builder.Build());
    htmlString = writer.ToString();
}
```
UseAutoIdentifiers enables adding ids for headers. 

UsePipeTables enables adding tables.

UseFigures enables usage of Figures statements.

UseCustomCodeBlockExtension is an extension written by us to replace default html code blob renderer with our own. 
This approach gives great possibilities for html syntax modifications for specified part of .md files, in this case parts with code samples.

As Markdig object model does not provide hierarchical object grouping like tree model, when extracting Headers there was need to write custom code.
```csharp
private void CreateTree(IEnumerable<HeadingBlock> headingBlocks, HeaderData root)
{
	// loop heading blocks and put them in hierarhical tree.
}
```

Markdig object model provides a collection of header objects of type HeadingBlock. They are all on the same level but in .md files we can deal with nested headers like:
```csharp
# 1. Main Header
	## 1.1. Nested Header
		### 1.1.1. Nested Header of Nested Header
# 2. Second Main Header
```
And so on.

So, in CreateTree method we look at every header and try to assign it to proper branch of our Header Tree structure.

### JSON serialization

When Markdown Data object is prepared, we need to serialize it into JSON format. We use standard JSON serialization library from [Newtonsoft](https://www.newtonsoft.com/json)
Additionaly to HtmlString and HeaderData we need to serialize Metadata dictionary. Some of fields here are mandatory so before we serialize object validation is performed.
```csharp
private Dictionary<string, string> ValidateMetaData(Dictionary<string, string> pairs)
{
    var validatedMetaData = new Dictionary<string, string>();
    CheckExistence("Title", pairs, validatedMetaData);
    CheckExistence("Author", pairs, validatedMetaData);
    CheckExistence("Published", pairs, validatedMetaData);
    foreach(var pair in pairs)
        validatedMetaData.Add(pair.Key, pair.Value);
    return validatedMetaData;
}
```
Title, Author and Published properties are required. So first we check if they are put in input dictionary passed as argument.
If not, we add them with empty value.
All other items from input dictionary is added at the end.

Here is the usage of serialization code:
```csharp
private string ConvertToJson(MarkdownData data)
{
    return JsonConvert.SerializeObject(data, Formatting.Indented, new JsonSerializerSettings
    {
        ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
        PreserveReferencesHandling = PreserveReferencesHandling.None
    });
}
```

In ProcessCommitAsync method there is one more step to do:
```csharp
return await Utilities.WriteJsonFilesToFileShareTask(jsonFiles,
    Utilities.GetEnvironmentVariable("AzureWebJobsStorage"),
    Utilities.GetEnvironmentVariable("OutputFileShareName"), log);
```

We need to store our new json files somewhere. We decided to choose Azure File Shares. There is a FileShare created only for this data.

The core of method is like this:
```csharp
var storageAccount = CloudStorageAccount.Parse(storageAccountConnectionString);
var fileClient = storageAccount.CreateCloudFileClient();
var share = fileClient.GetShareReference(GetEnvironmentVariable(fileShareName));

foreach (var json in jsonData)
{
    var sourceFile = share.GetRootDirectoryReference().GetFileReference(json.Item1);
    if (string.IsNullOrEmpty(json.Item2)) // when content is empty we should delete blob.
    {
        if (sourceFile.Exists())
            await sourceFile.DeleteAsync();
    }
    else
    {
        sourceFile.Properties.ContentType = "application/json; charset=utf-8";
        sourceFile.UploadText(json.Item2, Encoding.UTF8);
    }
}
```

There is also solution based on Azure Blobs which is not used but can be easly switched on.
```csharp
foreach (var json in jsonData)
{
    if (string.IsNullOrEmpty(json.Item2)) // when content is empty we should delete blob.
    {
        var blob =
            await binder.BindAsync<CloudBlockBlob>(new BlobAttribute($"json-container/{json.Item1}"));
        blob.Delete();
    }
    else
    {
        var blob =
            await binder.BindAsync<CloudBlockBlob>(new BlobAttribute($"json-container/{json.Item1}"));
        blob.Properties.ContentType = "application/json; charset=utf-8";
        blob.UploadText(json.Item2, Encoding.UTF8);
    }
}
```

You can see that if there is no content in Tuple Item2 file is considered as deleted so we delete it from container as well.
And if content is resent, we write it to blob names from Tuple Item1.

You can see here a "binder" object. It comes from the very start from "Run" method. And it's used for [imperative bindings](https://docs.microsoft.com/en-us/azure/azure-functions/functions-reference-csharp#imperative-bindings).
Very short, Azure Functions in general use declarative bindings. So you define inputs and outputs when defining Function itself.
Here we don't have this possibility because we don't know blob name at this point. 
For more details about imperative binding click [here](https://docs.microsoft.com/en-us/azure/azure-functions/functions-reference-csharp#imperative-bindings).

### Markdown parser in console app
There are scenarios where parser itself gets updated or changed. In that cases we would like to update all markdown files in specific repo according to new parser code. We could modify each md file and push those changes to GitHub which will fire Azure Function but it is not the best approach.
That's why we createad a console app which takes all markdown files from given repo and run parser against each of them. Resulting jsons are saved to FileShare in storage account given by the user.

Parameters:
```
-app         ApplicationName
-owner       GitHub repo owner
-name        GitHub repo name
-branch      GitHub branch name
-share       Name of File Share where result jsons will be stored
-connection  DataConnectionString to storage where result jsons will be stored
```
Data connection string is to be taken from Azure Portal in this form:
```
"DefaultEndpointsProtocol=https;AccountName=<account name>;AccountKey=<key>"
```
The code is similar to what MarkdownParserFunction provides. With this difference that we are looking for all markdown files in repo, not only those from latest commit.
So we use
```csharp
contents = await client.Repository.Content.GetAllContentsByRef(owner, repoName, branch);
```
to get all directories and files on root directory and then loop through them and use
```csharp
contents = await client.Repository.Content.GetAllContentsByRef(owner, repoName, path, branch);
```
where path is pointing to specific file or directory. 
Collection of all found markdown files is passed to parser as in Azure Function. 
Resulting Jsons are stored in FileShare defined by data connection string and share name parameters.
