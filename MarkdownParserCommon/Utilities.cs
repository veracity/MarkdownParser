using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace MarkdownParserCommon
{
    public class Utilities
    {
        /// <summary>
        /// Based on input list of tuples creates file for each.
        /// Name of the file is first string in tuple, content of file is second string in tuple
        /// </summary>
        /// <param name="jsonData">list of tuples with json data, item1-fileName, item2-json content</param>
        /// <param name="fileShareName">fileShare name</param>
        /// <param name="storageAccountConnectionString">connection string to storage account</param>
        /// <returns>true if success, false otherwise</returns>
        public static async Task<bool> WriteJsonFilesToFileShareTask(List<Tuple<string, string>> jsonData,
            string storageAccountConnectionString, string fileShareName)
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