using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace MarkdownParserCommon
{
    public class Utilities
    {
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

        /// <summary>
        /// Based on input list of tuples creates file for each.
        /// Name of the file is first string in tuple, content of file is second string in tuple
        /// </summary>
        /// <param name="jsonData">list of tuples with json data, item1-fileName, item2-json content</param>
        /// <param name="fileShareName">fileShare name</param>
        /// <param name="log">traceWriter for logging exceptions</param>
        /// <param name="storageAccountConnectionString">connection string to storage account</param>
        /// <returns>true if success, false otherwise</returns>
        public static async Task<bool> WriteJsonFilesToFileShareTask(List<Tuple<string, string>> jsonData,
            string storageAccountConnectionString, string fileShareName, TraceWriter log = null)
        {
            try
            {
                // by default functionapp storage account is used.
                var storageAccount = CloudStorageAccount.Parse(storageAccountConnectionString);
                var fileClient = storageAccount.CreateCloudFileClient();
                var share = fileClient.GetShareReference(fileShareName);

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
                return true;
            }
            catch (Exception e)
            {
                log?.Info("There was an exception thrown during writing json files to FileShare: " + e.Message);
                Console.WriteLine("There was an exception thrown during writing json files to FileShare: " + e.Message);
                return false;
            }
        }
        /// <summary>
        /// Find variable in current environment (e.g. app.config)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(
                       name, EnvironmentVariableTarget.Process);
        }
    }
}