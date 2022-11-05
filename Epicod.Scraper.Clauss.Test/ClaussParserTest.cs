using HtmlAgilityPack;
using System.IO;
using System.Reflection;
using System.Text;
using Xunit;

namespace Epicod.Scraper.Clauss.Test
{
    public sealed class ClaussParserTest
    {
        private static string LoadResourceText(string name)
        {
            using StreamReader reader = new(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream($"Epicod.Scraper.Clauss.Test.Assets.{name}")!,
                Encoding.UTF8);
            return reader.ReadToEnd();
        }

        [Fact]
        public void ParseInscriptionCount_Ok()
        {
            string html = LoadResourceText("Achaia.html");
            HtmlDocument doc = new();
            doc.LoadHtml(html);
            int id = 0;
            ClaussParser parser = new(() => ++id);

            int n = parser.ParseInscriptionCount(doc.DocumentNode);

            Assert.Equal(2031, n);
        }

        [Fact]
        public void ParseInscriptions_Ok()
        {
            string html = LoadResourceText("Achaia.html");
            HtmlDocument doc = new();
            doc.LoadHtml(html);
            int id = 0;
            ClaussParser parser = new(() => ++id);
            RamTextNodeWriter writer = new();

            int n = parser.ParseInscriptions(1, doc.DocumentNode, writer);

            Assert.Equal(64, n);
        }

        [Fact]
        public void ParseInscriptionsBInA_Ok()
        {
            string html = LoadResourceText("BInA.html");
            HtmlDocument doc = new();
            doc.LoadHtml(html);
            int id = 0;
            ClaussParser parser = new(() => ++id);
            RamTextNodeWriter writer = new();

            int n = parser.ParseInscriptions(1, doc.DocumentNode, writer);

            Assert.Equal(1, n);
            Assert.Equal(2, writer.Properties[1].Count);
            Assert.Contains(writer.Properties[1], p => p.Name == "publication");
            Assert.Contains(writer.Properties[1], p => p.Name == "image");
        }
    }
}