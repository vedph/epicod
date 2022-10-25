using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ScrapySharp.Html.Forms;

namespace Epicod.Scraper.Clauss
{
    public sealed class ClaussWebScraper : WebScraper, IWebScraper
    {
        public const string CORPUS = "clauss";

        public ClaussWebScraper(ITextNodeWriter writer) : base(writer)
        {
            Corpus = CORPUS;
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
            List<string> regions = new();
            foreach (HtmlNode node in homePage.Html.CssSelect(
                "form[name='provinzen'] input"))
            {
                string id = node.GetAttributeValue("value");
                if (!string.IsNullOrEmpty(id) && char.IsUpper(id[0]))
                    regions.Add(id);
            }

            // process each region
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

                WebPage regionPage = form.Submit(
                    new Uri("https://db.edcs.eu/epigr/epitest_ergebnis.php"),
                    HttpVerb.Post);

                // TODO
            }
        }
    }
}
