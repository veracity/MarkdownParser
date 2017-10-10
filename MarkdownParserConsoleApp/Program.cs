using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MarkdownParserCommon;
using Octokit;

namespace MarkdownParserConsoleApp
{
    /// <summary>
    /// Helper class to hold info from input arguments
    /// </summary>
    public class ArgsInfo
    {
        public string ConnectionString { get; set; }
        public string FileShareName { get; set; }
        public string RepoOwner { get; set; }
        public string RepoName { get; set; }
        public string AppName { get; set; }
        public string Branch { get; set; }
    }
    /// <summary>
    /// Main class. Executes operation.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Reads arguments and starts parse operation.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            Console.WriteLine("Starting...");
            var info = ParseArgs(args);
            if (info == null)
            {
                Console.WriteLine("Numer of arguments is not correct. Please check input parameters");
                ShowInstructions();
            }
            else
            {
                var task = Parse(info.AppName, info.RepoOwner, info.RepoName, info.Branch, info.ConnectionString,
                    info.FileShareName);
                task.Wait();
                Console.WriteLine(task.Result ? "Parsing finished successfully." : "Parsing failed.");
            }
        }
        /// <summary>
        /// Help for user in case of wrong input args
        /// </summary>
        private static void ShowInstructions()
        {
            Console.WriteLine("Usage: ");
            Console.WriteLine("-app         ApplicationName");
            Console.WriteLine("-owner       GitHub repo owner");
            Console.WriteLine("-name        GitHub repo name");
            Console.WriteLine("-branch      GitHub branch name");
            Console.WriteLine("-share       Name of File Share where result jsons will be stored");
            Console.WriteLine("-connection  DataConnectionString to storage where result jsons will be stored.");
        }
        /// <summary>
        /// Parsing args to ArgsInfo class.
        /// </summary>
        /// <param name="args">input array with args</param>
        /// <returns>ArgsInfo object</returns>
        private static ArgsInfo ParseArgs(string[] args)
        {
            var info =  new ArgsInfo();
            if (args.Length != 12) return null;
            try
            {
                for (var i = 0; i < args.Length; i++)
                {
                    if (args[i] == "-connection")
                        info.ConnectionString = args[i + 1];
                    if (args[i] == "-share")
                        info.FileShareName = args[i + 1];
                    if (args[i] == "-owner")
                        info.RepoOwner = args[i + 1];
                    if (args[i] == "-name")
                        info.RepoName = args[i + 1];
                    if (args[i] == "-app")
                        info.AppName = args[i + 1];
                    if (args[i] == "-branch")
                        info.Branch = args[i + 1];
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occured while reading input arguments. Details: {e}");
            }
            return info;
        }
        /// <summary>
        /// Parse method. Reads given repo. Finds all md files. Parses them to jsons. Saves to given File Share.
        /// </summary>
        /// <param name="appName">application name</param>
        /// <param name="owner">GitHub repo owner</param>
        /// <param name="repoName">GitHub repo name</param>
        /// <param name="branch">GitHub repo branch</param>
        /// <param name="storageAccConnectionString">data connection string to storage where jsons will be saved.</param>
        /// <param name="fileShareName">file Share name in storage</param>
        /// <returns>true if success. false if failed</returns>
        public static async Task<bool> Parse(string appName, string owner, string repoName, string branch,
            string storageAccConnectionString, string fileShareName)
        {
            var github = new GitHubClient(new ProductHeaderValue(appName));
            var mdFiles = new List<Tuple<string, string>>();
            Console.WriteLine("Finding all markdown files in repo...");
            mdFiles.AddRange(await GetMdFiles(github, owner, repoName, branch, string.Empty));
            var jsonList = new List<Tuple<string, string>>();
            Console.WriteLine($"{mdFiles.Count} items found. Parsing...");
            try
            {
                foreach (var mdFile in mdFiles)
                {
                    var mdParser = new MarkdownParser.MarkdownParser();
                    var jsonText = mdParser.CreateJson(mdFile.Item2);
                    jsonList.Add(new Tuple<string, string>($"{mdFile.Item1}.json", jsonText));
                    Console.WriteLine($"{mdFile.Item1} parsed.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occured while parsing markdown files. Details: {e}");
                return false;
            }
            Console.WriteLine("Writing jsons to file share...");
            return await Utilities.WriteJsonFilesToFileShareTask(jsonList, storageAccConnectionString, fileShareName);
        }
        /// <summary>
        /// Get all md files in given repo
        /// </summary>
        /// <param name="client">GitHub Octokit client</param>
        /// <param name="owner">GitHub repo owner</param>
        /// <param name="repoName">GitHub repo name</param>
        /// <param name="branch">GitHub repo branch</param>
        /// <param name="path">path to single item in repo</param>
        /// <returns>list of tuples with string - file name, string - file content</returns>
        public static async Task<List<Tuple<string, string>>> GetMdFiles(GitHubClient client, string owner,
            string repoName, string branch, string path)
        {
            var mdFiles = new List<Tuple<string, string>>();
            try
            {
                IReadOnlyList<RepositoryContent> contents;
                if (string.IsNullOrEmpty(path))
                    contents = await client.Repository.Content.GetAllContentsByRef(owner, repoName, branch);
                else
                    contents = await client.Repository.Content.GetAllContentsByRef(owner, repoName, path, branch);
                foreach (var content in contents)
                {
                    if (content.Type.Value == ContentType.File)
                    {
                        var ext = Path.GetExtension(content.Name);
                        if (ext != null && ext.Equals(".md"))
                        {
                            if (string.IsNullOrEmpty(content.Content))
                            {
                                var cont = await client.Repository.Content.GetAllContentsByRef(owner, repoName,
                                    content.Path,
                                    "master");
                                foreach (var con in cont)
                                    mdFiles.Add(new Tuple<string, string>(Path.GetFileNameWithoutExtension(con.Name),
                                        con.Content));
                            }
                            else
                                mdFiles.Add(new Tuple<string, string>(Path.GetFileNameWithoutExtension(content.Name),
                                    content.Content));
                        }
                    }
                    if (content.Type.Value == ContentType.Dir)
                        mdFiles.AddRange(await GetMdFiles(client, owner, repoName, branch, content.Path));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occured while getting markdown files from repo. Details: {e}");
            }
            return mdFiles;
        }
    }
}