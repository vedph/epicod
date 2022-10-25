using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Epicod.Scraper.Clauss
{
    public sealed class ClaussWebScraper : WebScraper, IWebScraper
    {
        public ClaussWebScraper(ITextNodeWriter writer) : base(writer)
        {
        }

        protected override async Task DoScrapeAsync()
        {
            // load root page
            ScrapingBrowser browser = new()
            {
                AllowAutoRedirect = true,
                IgnoreCookies = true
            };
            WebPage home = await browser.NavigateToPageAsync(new Uri(RootUri!));
            if (home == null) return;

            // scrape regions
            List<string> regions = new();
            foreach (HtmlNode node in home.Html.CssSelect(
                "form[name='provinzen'] > table > tbody td input"))
            {
                regions.Add(node.GetAttributeValue("value"));
            }

            // process each region
            foreach (string region in regions.OrderBy(s => s))
            {
                // TODO
            }
        }
    }
}
