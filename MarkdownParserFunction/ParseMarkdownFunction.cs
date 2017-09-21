using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using Octokit;
using Binder = Microsoft.Azure.WebJobs.Binder;

namespace MarkdownParserFunction
{
    /// <summary>
    /// This function translates all Markdown files from current commit to coresponding json files
    /// containing html parsed from md and additional metadata like headers tree structure
    /// </summary>
    public static class ParseMarkdownFunction
    {
        /// <summary>
        /// Azure Function entry point. Executed by trigger from GitHub Webhook.
        /// Json message structure from GitHub Webhook:
        /// https://developer.github.com/v3/activity/events/types/#pushevent 
        /// Imperative bindings reference:
        /// https://docs.microsoft.com/en-us/azure/azure-functions/functions-reference-csharp#imperative-bindings
        /// </summary>
        /// <param name="req">message recieved from GitHub</param>
        /// <param name="binder">Binder used for imperative bindings</param>
        /// <param name="log">TraceWriter for logging exceptions</param>
        /// <returns></returns>
        [FunctionName("ParseMarkdownFunction")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(WebHookType = "github")] HttpRequestMessage req,
            Binder binder, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");
            try
            {
                dynamic data = await req.Content.ReadAsAsync<object>();
                var result = await ProcessCommitAsync(data, binder, log);
                return result
                    ? req.CreateResponse(HttpStatusCode.OK)
                    : req.CreateErrorResponse(HttpStatusCode.NoContent, "error.");
            }
            catch (ReflectionTypeLoadException ex)
            {
                var sb = new StringBuilder();
                foreach (var exSub in ex.LoaderExceptions)
                {
                    sb.AppendLine(exSub.Message);
                    var exFileNotFound = exSub as FileNotFoundException;
                    if (!string.IsNullOrEmpty(exFileNotFound?.FusionLog))
                    {
                        sb.AppendLine("Fusion Log:");
                        sb.AppendLine(exFileNotFound.FusionLog);
                    }
                    sb.AppendLine();
                }
                return req.CreateErrorResponse(HttpStatusCode.NoContent, sb.ToString());
            }
        }
        /// <summary>
        /// Get all necessary info from GitHub json message
        /// Perform function operations.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="binder"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static async Task<bool> ProcessCommitAsync(dynamic data, Binder binder, TraceWriter log)
        {
            var repositoryId = (long) data.repository.id;
            var branch = (string) data.repository.default_branch;
            var commits = new List<string>();
            foreach (var commit in data.commits)
                commits.Add(commit.id);

            commits.Add(data.head_commit.id);
            var mdFiles = await GetAllMdFilesTask("MarkdownParser", repositoryId, branch, commits, log);
            var jsonFiles = PrepareJsonData(mdFiles, log);
            return await WriteJsonFilesToBlobsTask(jsonFiles, binder, log);
        }
        /// <summary>
        /// Use Octokit to connect to GitHub and retrieve information about current commit.
        /// </summary>
        /// <param name="appName">Needed by GitHubClient, application name</param>
        /// <param name="repositoryId">Repository where current commit happened</param>
        /// <param name="branchName">Name of the branch of current commit</param>
        /// <param name="commitIds">Id of current commits</param>
        /// <param name="log">TraceWriter for logging failures if happen</param>
        /// <returns>List of tuple with two strings. First string is the name of file, second - content</returns>
        public static async Task<List<Tuple<string, string>>> GetAllMdFilesTask(string appName, long repositoryId,
            string branchName, List<string> commitIds, TraceWriter log)
        {
            var mdFiles = new List<Tuple<string, string>>();
            try
            {
                var github = new GitHubClient(new ProductHeaderValue(appName));
                var files = new List<GitHubCommitFile>();
                foreach (var id in commitIds)
                {
                    var commit = await github.Repository.Commit.Get(repositoryId, id);
                    files.AddRange(commit.Files.ToList());
                }
                foreach (var file in files)
                {
                    if (mdFiles.FirstOrDefault(m => m.Item1.Equals(Path.GetFileNameWithoutExtension(file.Filename))) != null)
                        continue;
                    var ext = Path.GetExtension(file.Filename);
                    if (ext != null && ext.Equals(".md"))
                    {
                        // when status == "removed" there is no file available and GetAllContentsByRef throws exception
                        if (file.Status == "modified" || file.Status == "added") 
                        {
                            var contents =
                                await github.Repository.Content.GetAllContentsByRef(repositoryId, file.Filename,
                                    branchName);
                            if (contents != null)
                                foreach (var content in contents)
                                    mdFiles.Add(
                                        new Tuple<string, string>(Path.GetFileNameWithoutExtension(content.Name),
                                            content.Content));
                        }
                        else // add this anyway with content = string.empty which will cause deletion of this blob in next steps
                            mdFiles.Add(new Tuple<string, string>(Path.GetFileNameWithoutExtension(file.Filename),
                                string.Empty));
                    }
                }
            }
            catch (Exception e)
            {
                log.Info("There was an exception thrown during downloading md files from GitHub: " + e.Message);
                return new List<Tuple<string, string>>();
            }
            return mdFiles;
        }
        /// <summary>
        /// Prepare json with MdParser.
        /// </summary>
        /// <param name="mdFiles">list tuples with md files data</param>
        /// <param name="log">traceWriter for logging exceptions</param>
        /// <returns>List of tuple with two strings. First is fileName, second is json content</returns>
        public static List<Tuple<string, string>> PrepareJsonData(List<Tuple<string, string>> mdFiles,
            TraceWriter log)
        {
            var jsonList = new List<Tuple<string, string>>();
            try
            {
                foreach (var mdFile in mdFiles)
                {
                    var mdParser = new MarkdownParser.MarkdownParser();
                    var jsonText = mdParser.CreateJson(mdFile.Item2);
                    jsonList.Add(new Tuple<string, string>($"{mdFile.Item1}.json", jsonText));
                }
            }
            catch (Exception e)
            {
                log.Info("There was an exception thrown during preparation of Json data: " + e.Message);
                return new List<Tuple<string, string>>();
            }
            return jsonList;
        }
        /// <summary>
        /// Based on input list of tuples creates blob for each.
        /// Name of the blob is first string in tuple, content of blob is second string in tuple
        /// Imperative bindings used here:
        /// https://docs.microsoft.com/en-us/azure/azure-functions/functions-reference-csharp#imperative-bindings
        /// </summary>
        /// <param name="jsonData">list of tuples with json data, item1-fileName, item2-json content</param>
        /// <param name="binder">Binder used for imperative bindings</param>
        /// <param name="log">traceWriter for logging exceptions</param>
        /// <returns>true if success, false otherwise</returns>
        public static async Task<bool> WriteJsonFilesToBlobsTask(List<Tuple<string, string>> jsonData, Binder binder,
            TraceWriter log)
        {
            try
            {
                // by default functionapp storage account is used.
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
                return true;
            }
            catch (Exception e)
            {
                log.Info("There was an exception thrown during writing json files to blobs: " + e.Message);
                return false;
            }
        }
    }
}