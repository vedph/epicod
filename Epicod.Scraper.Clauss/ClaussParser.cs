using Epicod.Core;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using ScrapySharp.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Epicod.Scraper.Clauss
{
    public sealed class ClaussParser
    {
        private readonly Func<int> _idGenerator;
        private readonly Regex _wsRegex;
        private readonly Regex _endDigitsRegex;
        private readonly Regex _metaRegex;
        private readonly Regex _targetRegex;
        private readonly Regex _aRegex;
        private readonly Regex _latRegex;
        private readonly Regex _lonRegex;
        private readonly Regex _btnRegex;

        public ILogger? Logger { get; set; }

        public ClaussParser(Func<int> idGenerator)
        {
            _idGenerator = idGenerator ??
                throw new ArgumentNullException(nameof(idGenerator));

            _wsRegex = new Regex(@"\s+", RegexOptions.Compiled);
            _endDigitsRegex = new Regex(@"([0-9]+)\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
            _metaRegex = new Regex(@"^([^<:]+):?</b>(.*)");
            _targetRegex = new Regex(@"\s+target=""_blank""", RegexOptions.Compiled);
            _aRegex = new Regex(@"<a\s+href=""([^""]+)"">([^<]+)</a>", RegexOptions.Compiled);
            _latRegex = new Regex(@"latitude=([-.0-9]+)", RegexOptions.Compiled);
            _lonRegex = new Regex(@"longitude=([-.0-9]+)", RegexOptions.Compiled);
            _btnRegex = new Regex(@"<a\s+href=""https://db.edcs.eu/epigr/partner.php.*?</a>",
                RegexOptions.Compiled);
        }

        public static IList<string> ParseRegions(HtmlNode htmlNode)
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
            if (htmlNode is null) throw new ArgumentNullException(nameof(htmlNode));

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

        private void ParseMetadatum(string label, string content,
            IList<TextNodeProperty> properties)
        {
            TextNodeProperty p = new()
            {
                Name = label
            };
            Match m;
            switch (p.Name)
            {
                case "publication":
                    // purge from button
                    content = _btnRegex.Replace(content, "").Trim();
                    if (content.IndexOf('<') == -1)
                    {
                        p.Value = content;
                        properties.Add(p);
                    }
                    else
                    {
                        // <a href="image-link-via-script">csv list</a>
                        content = _targetRegex.Replace(content, "");
                        m = _aRegex.Match(content);
                        if (m.Success)
                        {
                            p.Value = m.Groups[2].Value;
                            properties.Add(p);
                            properties.Add(new TextNodeProperty
                            {
                                Name = "image",
                                Value = m.Groups[1].Value
                            });
                        }
                        else
                        {
                            Logger?.LogError("Unexpected element in publication: "
                                + content);
                            p.Value = content;
                            properties.Add(p);
                        }
                    }
                    break;

                case "place":
                    if (content.IndexOf('<') == -1)
                    {
                        p.Value = content;
                        properties.Add(p);
                    }
                    else
                    {
                        // script, a, noscript: just extract from first <a>
                        // the place name and its lat/lon from href
                        content = _targetRegex.Replace(content, "");
                        m = _aRegex.Match(content);
                        if (m.Success)
                        {
                            // rename place as location
                            p.Name = "location";
                            p.Value = m.Groups[2].Value;
                            properties.Add(p);
                            // extract lat and lon from href
                            properties.Add(new TextNodeProperty
                            {
                                Name = "lat",
                                Value = _latRegex.Match(m.Groups[1].Value).Groups[1].Value
                            });
                            properties.Add(new TextNodeProperty
                            {
                                Name = "lon",
                                Value = _lonRegex.Match(m.Groups[1].Value).Groups[1].Value
                            });
                        }
                        else
                        {
                            Logger?.LogError("Unexpected element in place: "
                                + content);
                            p.Value = content;
                        }
                    }
                    break;

                case "inscription genus / personal status":
                    // tags, semicolon delimited
                    foreach (string tag in content.Split(';',
                        StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim()).Where(s => s.Length > 0))
                    {
                        properties.Add(new TextNodeProperty
                        {
                            Name = "tag",
                            Value = tag
                        });
                    }
                    break;

                default:
                    if (content.IndexOf('<') > -1)
                        Logger?.LogError($"Unexpected element in {p.Name}: " + content);
                    p.Value = content;
                    properties.Add(p);
                    break;
            }
        }

        public int ParseInscriptions(int parentNodeId, HtmlNode htmlNode,
            ITextNodeWriter? writer = null)
        {
            if (htmlNode is null) throw new ArgumentNullException(nameof(htmlNode));

            Logger?.LogInformation($"[B] Inscriptions (#{parentNodeId})");
            int x = 0;
            List<TextNodeProperty> props = new();

            // each body/p (except last) is an inscription
            foreach (HtmlNode p in htmlNode.SelectNodes(
                "html/body/p[position() < last()]"))
            {
                // extract its inner HTML and normalize it
                string html = p.InnerHtml.Replace("&nbsp;", " ");
                html = _wsRegex.Replace(html, " ").Trim();

                // create inscription node
                TextNode node = new()
                {
                    Id = _idGenerator.Invoke(),
                    Corpus = ClaussWebScraper.CORPUS,
                    Y = 2,
                    X = ++x,
                    ParentId = parentNodeId
                };

                // parse properties
                foreach (string part in html.Split("<br>").Select(s => s.Trim()))
                {
                    // text starts without tags
                    if (!part.StartsWith("<", StringComparison.Ordinal))
                    {
                        props.Add(new TextNodeProperty
                        {
                            Name = "text",
                            Value = part.Trim()
                        });
                    }
                    // else metadata: <b>NAME:</b> VALUE...
                    else
                    {
                        foreach (string meta in part.Split("<b>"))
                        {
                            Match m = _metaRegex.Match(meta);
                            if (m.Success)
                            {
                                ParseMetadatum(m.Groups[1].Value.Trim(),
                                    m.Groups[2].Value.Trim(),
                                    props);
                            }
                        }
                    }
                }

                // write node
                node.Name = props.Find(p => p.Name == "publication")?.Value ?? "";
                writer?.Write(node, props.ToArray());

                props.Clear();
            }
            return x;
        }
    }
}
