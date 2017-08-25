using System.Collections.Generic;
using System.IO;
using System.Linq;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.SyntaxHighlighting;
using Newtonsoft.Json;

namespace MarkdownParser
{
    /// <summary>
    /// Class reponsible for parsing Markdown file into json containing parsed html and 
    /// additional metadata like headers tree structure
    /// </summary>
    public class MarkdownParser
    {
        /// <summary>
        /// Prepare json based on md file input
        /// </summary>
        /// <param name="mdFileContent">md file content string</param>
        /// <returns>json string</returns>
        public string CreateJson(string mdFileContent)
        {
            if (string.IsNullOrEmpty(mdFileContent)) return string.Empty;
            return ConvertToJson(PrepareMarkdownData(mdFileContent));
        }
        /// <summary>
        /// Prepare pure markdownData containing parsed html and additional metadata
        /// </summary>
        /// <param name="mdFileContent"></param>
        /// <returns>Json structure class</returns>
        public MarkdownData PrepareMarkdownData(string mdFileContent)
        {
            MarkdownDocument document;
            string htmlString;
            using (var writer = new StringWriter())
            {
                var builder = new MarkdownPipelineBuilder();
                builder.UseAutoIdentifiers()
                    .UseAdvancedExtensions()
                    .UseSyntaxHighlighting();
                document = Markdown.ToHtml(mdFileContent, writer, builder.Build());
                htmlString = writer.ToString();
            }
            var head = new HeaderData { Level = 0 };
            CreateTree(document.Descendants<HeadingBlock>().ToList(), head);
            return new MarkdownData
            {
                HtmlString = htmlString,
                HeaderData = Compress(head.Children)
            };
        }
        /// <summary>
        /// Create tree structure from non tree structure Markdown document based on order.
        /// </summary>
        /// <param name="headingBlocks">collection of available headers</param>
        /// <param name="root">root container for tree structure</param>
        private void CreateTree(IEnumerable<HeadingBlock> headingBlocks, HeaderData root)
        {
            HeaderData currentHeader = root;
            foreach (var block in headingBlocks)
            {
                if (currentHeader == null) break;
                var diff = block.Level - currentHeader.Level;
                // next item can belong to one of the parents if level diff is negative.
                // here find proper parent
                if (diff <= 0)
                {
                    while (true)
                    {
                        currentHeader = currentHeader.Parent;
                        if (currentHeader == null) break;
                        if (block.Level - currentHeader.Level > 0)
                        {
                            diff = block.Level - currentHeader.Level;
                            break;
                        }
                    }
                }
                if (diff > 0)
                    currentHeader?.Children.Add(CreateTreeItem(currentHeader, diff, block, out currentHeader));
            }
        }
        /// <summary>
        /// Create tree item or several tree items in parent-child relation based on levels count.
        /// There might be situations where document is invalid and header2 is followed by header4
        /// then to keep tree structure we need to create fake header3 in the middle.
        /// </summary>
        /// <param name="currentHeader">current header</param>
        /// <param name="levels">int representation of how many levels there is
        /// between curent header and last one added</param>
        /// <param name="headingBlock">data source for tree item</param>
        /// <param name="lastItemAdded">item added previously to tree</param>
        /// <returns>tree structure item for heading block</returns>
        private HeaderData CreateTreeItem(HeaderData currentHeader, int levels, HeadingBlock headingBlock, out HeaderData lastItemAdded)
        {
            var result = new List<HeaderData>();
            for (var i = 0; i < levels; i++)
            {
                result.Add(new HeaderData { Level = currentHeader.Level + 1 + i });
                if (i == levels - 1)
                {
                    result[i].Text = headingBlock.Inline.FirstChild.ToString();
                    result[i].Level = headingBlock.Level;
                    result[i].Id = headingBlock.GetAttributes().Id;
                }
                if (i > 0)
                {
                    result[i - 1].Children = new List<HeaderData> { result[i] };
                    result[i].Parent = result[i - 1];
                }
                else
                    result[i].Parent = currentHeader;
            }
            lastItemAdded = result.Last();
            return result[0];
        }
        /// <summary>
        /// Switch from HeaderData to Header to ommit all properties not needed in JSON but needed 
        /// when building a tree structure
        /// </summary>
        /// <param name="data">header data to compress</param>
        /// <returns>Header object containing only necessary items for json</returns>
        private List<Header> Compress(List<HeaderData> data)
        {
            var returnList = new List<Header>();
            foreach (var item in data)
                returnList.Add(new Header { Id = item.Id, Text = item.Text, Children = Compress(item.Children) });
            return returnList;
        }
        /// <summary>
        /// JSON conversion.
        /// </summary>
        /// <param name="data">object containing data for json serialization in wanted structure</param>
        /// <returns>json string</returns>
        private string ConvertToJson(MarkdownData data)
        {
            return JsonConvert.SerializeObject(data, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                PreserveReferencesHandling = PreserveReferencesHandling.None
            });
        }
    }
    /// <summary>
    /// This is how Json will look like after serialization.
    /// </summary>
    public class MarkdownData
    {
        /// <summary>
        /// TODO: Find where from take the title. For now leave it empty
        /// </summary>
        public string Title { get; set; } = string.Empty;
        public string HtmlString { get; set; }
        public List<Header> HeaderData { get; set; }
    }
    /// <summary>
    /// For JSON serialization, cheaper version of HeaderData
    /// Only info that is needed.
    /// </summary>
    public class Header
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public List<Header> Children { get; set; }
    }
    /// <summary>
    /// For tree structure builder.
    /// </summary>
    public class HeaderData
    {
        public HeaderData() { Children = new List<HeaderData>(); }
        public string Id { get; set; }
        public string Text { get; set; }
        public List<HeaderData> Children { get; set; }
        public int Level { get; set; }
        public HeaderData Parent { get; set; }
    }
}