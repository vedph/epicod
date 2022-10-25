using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ScrapySharp.Html.Forms;
using Epicod.Core;

namespace Epicod.Scraper.Clauss
{
    public sealed class ClaussWebScraper : WebScraper, IWebScraper
    {
        public const string CORPUS = "clauss";
        private readonly ClaussParser _parser;

        public ClaussWebScraper(ITextNodeWriter writer) : base(writer)
        {
            Corpus = CORPUS;
            _parser = new ClaussParser { Logger = Logger };
        }

        protected override async Task DoScrapeAsync()
        {
            // load root page
            ScrapingBrowser browser = new()
            {
                AllowAutoRedirect = true,
                IgnoreCookies = true
            };
            WebPage homePage = await browser.NavigateToPageAsync(new Uri(RootUri!));
            if (homePage == null) return;

            // scrape regions
            IList<string> regions = _parser.ParseRegions(homePage.Html);

            // for each region (in alphabetical order)
            int x = 1;
            foreach (string region in regions.OrderBy(s => s))
            {
                Logger?.LogInformation("[A] Region: " + region);

                PageWebForm form = homePage.FindForm("epi");
                form["p_provinz"] = "Galatia";
                form["s_sprache"] = "en";
                form["r_auswahl"] = "und";
                form["r_sortierung"] = "Provinz";
                form["cmd_submit"] = "go";
                form.Method = HttpVerb.Post;

                // load region page
                WebPage regionPage = form.Submit(
                    new Uri("https://db.edcs.eu/epigr/epitest_ergebnis.php"),
                    HttpVerb.Post);

                // parse expected count
                int expectedCount = _parser.ParseInscriptionCount(regionPage.Html);
                Logger?.LogInformation($"Expected count: {expectedCount}");

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
                WriteNode(regionNode, new List<TextNodeProperty>()
                {
                    new TextNodeProperty(regionNode.Id,
                        "count",
                        $"{expectedCount}",
                        "integer")
                });

                // TODO
            }
        }
    }
}
