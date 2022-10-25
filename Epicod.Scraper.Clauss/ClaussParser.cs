using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using ScrapySharp.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Epicod.Scraper.Clauss
{
    public sealed class ClaussParser
    {
        private readonly Regex _endDigitsRegex;

        public ILogger? Logger { get; set; }

        public ClaussParser()
        {
            _endDigitsRegex = new Regex(@"([0-9]+)\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        public IList<string> ParseRegions(HtmlNode htmlNode)
        {
            if (htmlNode is null) throw new ArgumentNullException(nameof(htmlNode));

            // scrape regions
            List<string> regions = new();
            foreach (HtmlNode node in htmlNode.CssSelect(
                "form[name='provinzen'] input"))
            {
                string id = node.GetAttributeValue("value");
                if (!string.IsNullOrEmpty(id) && char.IsUpper(id[0]))
                    regions.Add(id);
            }

            return regions;
        }

        public int ParseInscriptionCount(HtmlNode htmlNode)
        {
            HtmlNode? node = htmlNode.SelectSingleNode(
                "//h3/p/b[starts-with(text(), 'inscriptions found')]");
            if (node != null)
            {
                Match m = _endDigitsRegex.Match(node.InnerText);
                if (m.Success)
                    return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            Logger?.LogError("Expected inscriptions count not found");
            return 0;
        }
    }
}
