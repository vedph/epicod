using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ScrapySharp.Html.Forms;
using Epicod.Core;
using System.Threading;
using System.Diagnostics;

namespace Epicod.Scraper.Clauss
{
    public sealed class ClaussWebScraper : WebScraper, IWebScraper
    {
        public const string CORPUS = "clauss";
        private readonly ClaussParser _parser;

        public ClaussWebScraper(ITextNodeWriter writer) : base(writer)
        {
            Corpus = CORPUS;
            _parser = new ClaussParser(GetNextNodeId) { Logger = Logger };
        }

        protected override async Task DoScrapeAsync()
        {
            _parser.Progress = Progress;

            // load root page
            ScrapingBrowser browser = new()
            {
                AllowAutoRedirect = true,
                IgnoreCookies = true
            };
            WebPage homePage = await browser.NavigateToPageAsync(new Uri(RootUri!));
            if (homePage == null) return;

            // scrape regions
            IList<string> regions = ClaussParser.ParseRegions(homePage.Html);

            // for each region (in alphabetical order)
            int x = 1;
            TimeSpan ts = TimeSpan.FromSeconds(0);
            foreach (string region in regions.OrderBy(s => s))
            {
                Logger?.LogInformation("[A] Region: {Region}", region);
                if (Delay > 0) Thread.Sleep(Delay);

                PageWebForm form = homePage.FindForm("epi");
                form["p_provinz"] = region;
                form["s_sprache"] = "en";
                form["r_auswahl"] = "und";
                form["r_sortierung"] = "Provinz";
                form["cmd_submit"] = "go";
                form.Method = HttpVerb.Post;

                // load region page
                Logger?.LogInformation("{Now} Loading page...",
                    DateTime.Now.ToString("HH:mm:ss"));
                Stopwatch stopwatch = new();
                stopwatch.Start();
                WebPage regionPage = form.Submit(
                    new Uri("https://db.edcs.eu/epigr/epitest_ergebnis.php"),
                    HttpVerb.Post);
                stopwatch.Stop();
                ts += stopwatch.Elapsed;
                Logger?.LogInformation("Page loaded in {Time}", stopwatch.Elapsed);

                // parse expected count
                int expectedCount = _parser.ParseInscriptionCount(regionPage.Html);
                Logger?.LogInformation("Expected count: {ExpectedCount}",
                    expectedCount);

                // write region node
                TextNode regionNode = new()
                {
                    Id = GetNextNodeId(),
                    Corpus = CORPUS,
                    Name = region,
                    ParentId = 0,
                    Y = 1,
                    X = x++
                };
                if (!IsDry)
                {
                    WriteNode(regionNode, new List<TextNodeProperty>()
                    {
                        new TextNodeProperty(regionNode.Id,
                            "count",
                            $"{expectedCount}",
                            "integer")
                    });
                }

                // parse inscriptions in page
                int actualCount = _parser.ParseInscriptions(regionNode.Id,
                    regionPage.Html, IsDry? null : Writer);
                if (actualCount != expectedCount)
                {
                    Logger?.LogError("Actual inscriptions count ({ActualCount}) " +
                        "does not match expected count ({ExpectedCount})",
                        actualCount, expectedCount);
                }
            }

            Logger?.LogInformation("Total page load time: {Time}", ts);
        }
    }
}
