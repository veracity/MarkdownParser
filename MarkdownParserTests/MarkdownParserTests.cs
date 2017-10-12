using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MarkdownParser;
using NUnit.Framework;

namespace MarkdownParserTests
{
    [TestFixture]   
    public class MarkdownParserTests
    {
        [Test]
        public void TestParseMarkdownData()
        {
            var dir = Path.GetDirectoryName(new Uri(typeof(MarkdownParserTests).Assembly.CodeBase).LocalPath);
            Assert.NotNull(dir);
            var parser = new MarkdownParser.MarkdownParser();
            var pathToMd = Path.Combine(dir, @"Cases\fileMdHeaders.md");
            MarkdownData result;
            using (var reader = new StreamReader(pathToMd, Encoding.UTF8))
                result = parser.PrepareMarkdownData(reader.ReadToEnd());
            Assert.AreEqual(1, result.HeaderData.Count);
            Assert.AreEqual(5, result.HeaderData[0].Children.Count);
            Assert.AreEqual(2, result.HeaderData[0].Children[1].Children.Count);
            Assert.IsNull(result.HeaderData[0].Children[4].Children[0].Children[0].Children[0].Id);
            Assert.AreEqual("sub-sub-header6", result.HeaderData[0].Children[4].Children[0].Children[0].Children[0].Children[0].Id);
            Assert.NotNull(result.MetaData);
            Assert.True(result.MetaData.Count ==4);
            Assert.AreEqual("myTitle", result.MetaData["Title"]);
            Assert.True(result.MetaData.ContainsKey("Author"));
            Assert.True(result.MetaData.ContainsKey("Published"));
        }
        [Test]
        public void TestParseMarkdownData2()
        {
            var dir = Path.GetDirectoryName(new Uri(typeof(MarkdownParserTests).Assembly.CodeBase).LocalPath);
            Assert.NotNull(dir);
            var parser = new MarkdownParser.MarkdownParser();
            var pathToMd = Path.Combine(dir, @"Cases\fileMdHeaders1.md");
            MarkdownData result;
            using (var reader = new StreamReader(pathToMd, Encoding.UTF8))
                result = parser.PrepareMarkdownData(reader.ReadToEnd());
            Assert.AreEqual(1, result.HeaderData.Count);
            Assert.IsNull(result.HeaderData[0].Id);
            Assert.IsNull(result.HeaderData[0].Children[0].Id);
            Assert.AreEqual("subheader-four", result.HeaderData[0].Children[0].Children[0].Id);
            Assert.AreEqual("sub-sub-header", result.HeaderData[0].Children[0].Children[0].Children[0].Id);
            Assert.IsNull(result.HeaderData[0].Children[0].Children[0].Children[0].Children[0].Id);
            Assert.AreEqual("sub-sub-header6", result.HeaderData[0].Children[0].Children[0].Children[0].Children[0].Children[0].Id);
            Assert.NotNull(result.MetaData);
            Assert.True(result.MetaData.Count == 3);
        }
        [Test]
        public void TestParseMarkdownDataWithContent()
        {
            var dir = Path.GetDirectoryName(new Uri(typeof(MarkdownParserTests).Assembly.CodeBase).LocalPath);
            Assert.NotNull(dir);
            var parser = new MarkdownParser.MarkdownParser();
            var pathToMd = Path.Combine(dir, @"Cases\fileMdHeadersWithContent.md");
            MarkdownData result;
            using (var reader = new StreamReader(pathToMd, Encoding.UTF8))
                result = parser.PrepareMarkdownData(reader.ReadToEnd());
            Assert.AreEqual(1, result.HeaderData.Count);
            Assert.IsNull(result.HeaderData[0].Id);
            Assert.AreEqual(5, result.HeaderData[0].Children.Count);
            Assert.AreEqual("active-directory-b2c-wordpress-plugin-openidconnect", result.HeaderData[0].Children[0].Id);
            Assert.AreEqual(0, result.HeaderData[0].Children[0].Children.Count);
            Assert.AreEqual("more-information", result.HeaderData[0].Children[4].Id);
            Assert.NotNull(result.MetaData);
            Assert.True(result.MetaData.Count == 3);
        }

        [Test]
        public void TestParseMarkdownDataWithCodeContent()
        {
            var dir = Path.GetDirectoryName(new Uri(typeof(MarkdownParserTests).Assembly.CodeBase).LocalPath);
            Assert.NotNull(dir);
            var parser = new MarkdownParser.MarkdownParser();
            var pathToMd = Path.Combine(dir, @"Cases\fileMdHeadersWithCodeSample.md");
            MarkdownData result;
            using (var reader = new StreamReader(pathToMd, Encoding.UTF8))
                result = parser.PrepareMarkdownData(reader.ReadToEnd());
            Assert.True(result.HtmlString.Contains("javascript"));
            Assert.True(result.HtmlString.Contains("python"));
            Assert.True(result.HtmlString.Contains("veracity-dev-pres-html-code"));
            Assert.True(result.HtmlString.Contains("<strong></strong>"));
            Assert.False(result.HtmlString.Contains("UNKNOWN"));
            Assert.AreEqual(3, result.HeaderData.Count);
            Assert.AreEqual("overview", result.HeaderData[0].Id);
            Assert.AreEqual(0, result.HeaderData[0].Children.Count);
            Assert.NotNull(result.MetaData);
            Assert.True(result.MetaData.Count == 3);
        }

        [Test, Ignore("Used to parse few file from local drive")]
        public void ParseFiles()
        {
            var files = new List<string>
            {
                @"C:\Data\pawe\VS\Markdown files\dataquality.md",
                @"C:\Data\pawe\VS\Markdown files\template1 with long overview.md",
                @"C:\Data\pawe\VS\Markdown files\template2 with short overview.md",
                @"C:\Data\pawe\VS\Markdown files\template3 with code example.md"
            };
            foreach (var file in files)
            {
                var parser = new MarkdownParser.MarkdownParser();
                using (var reader = new StreamReader(file, Encoding.UTF8))
                {
                    var json = parser.CreateJson(reader.ReadToEnd());
                    File.WriteAllText(
                        Path.Combine(@"C:\Data\pawe\VS\Markdown files",
                            string.Join(".", Path.GetFileNameWithoutExtension(file), "json")), json);
                }
            }
        }
    }
}
