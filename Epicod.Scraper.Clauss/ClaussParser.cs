using Epicod.Core;
using Fusi.Tools;
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
        private readonly Regex _detailsRegex;

        public ILogger? Logger { get; set; }

        public IProgress<ProgressReport>? Progress { get; set; }

        public ClaussParser(Func<int> idGenerator)
        {
            _idGenerator = idGenerator ??
                throw new ArgumentNullException(nameof(idGenerator));

            _wsRegex = new Regex(@"\s+", RegexOptions.Compiled);
            _endDigitsRegex = new Regex(@"([0-9]+)\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
            _metaRegex = new Regex("^([^<:]+):?</b>(.*)");
            _targetRegex = new Regex(@"\s+target=""_blank""", RegexOptions.Compiled);
            _aRegex = new Regex(@"<a\s+href=""([^""]+)"">([^<]+)</a>",
                RegexOptions.Compiled);
            _latRegex = new Regex(@"latitude=\s*'\s*?([-.0-9]+)",
                RegexOptions.Compiled);
            _lonRegex = new Regex(@"longitude=\s*'\s*?([-.0-9]+)",
                RegexOptions.Compiled);
            _btnRegex = new Regex(
                @"<a\s+href=""(?:https://db.edcs.eu/epigr/)?partner.php.*?</a>",
                RegexOptions.Compiled);
            _detailsRegex = new Regex("<details[^>]*>(.*?)</details>",
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

        private Tuple<double, double>? ParsePoint(string text)
        {
            Match lat = _latRegex.Match(text);
            if (!lat.Success) return null;
            Match lon = _lonRegex.Match(text);
            if (!lon.Success) return null;

            return Tuple.Create(
                double.Parse(lon.Groups[1].Value, CultureInfo.InvariantCulture),
                double.Parse(lat.Groups[1].Value, CultureInfo.InvariantCulture));
        }

        private void ParseMetadatum(int nodeId, string label, string content,
            IList<TextNodeProperty> properties)
        {
            TextNodeProperty p = new()
            {
                NodeId = nodeId,
                Name = label.ToLowerInvariant()
            };
            Match m;
            switch (p.Name)
            {
                case "publication":
                    // purge from button
                    content = _btnRegex.Replace(content, "").Trim();
                    if (!content.Contains('<'))
                    {
                        p.Value = _wsRegex.Replace(content, " ").Trim();
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
                                NodeId = nodeId,
                                Name = "image",
                                Value = m.Groups[1].Value
                            });
                        }
                        else
                        {
                            Logger?.LogError("Unexpected element in publication: {Content}",
                                content);
                            p.Value = _wsRegex.Replace(content, " ").Trim();
                            properties.Add(p);
                        }
                    }
                    break;

                case "place":
                    if (!content.Contains('<'))
                    {
                        p.Value = _wsRegex.Replace(content, " ").Trim();
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
                            var pt = ParsePoint(m.Groups[1].Value);
                            if (pt == null)
                            {
                                Logger?.LogError("Coordinates not found in {Href}",
                                    m.Groups[1].Value);
                            }
                            else
                            {
                                properties.Add(new TextNodeProperty
                                {
                                    NodeId = nodeId,
                                    Name = "point",
                                    Value = $"POINT({pt.Item1},{pt.Item2})",
                                    Type = "point"
                                });
                            }
                        }
                        else
                        {
                            Logger?.LogError("Unexpected element in place: {Content}",
                                content);
                            p.Value = _wsRegex.Replace(content, " ").Trim();
                            properties.Add(p);
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
                            NodeId = nodeId,
                            Name = "tag",
                            Value = tag
                        });
                    }
                    break;

                case "comment":
                    p.Value = _wsRegex.Replace(content, " ").Trim();
                    properties.Add(p);
                    break;

                default:
                    if (content.IndexOf('<') > -1)
                    {
                        Logger?.LogError("Unexpected element in {Name}: {Content}",
                            p.Name, content);
                    }
                    p.Value = _wsRegex.Replace(content, " ").Trim();
                    properties.Add(p);
                    break;
            }
        }

        private void UnwrapBInA(HtmlNode htmlNode)
        {
            foreach (HtmlNode b in htmlNode.SelectNodes(".//a/b")?.ToArray()
                ?? Array.Empty<HtmlNode>())
            {
                b.ParentNode.ReplaceChild(HtmlNode.CreateNode(b.InnerHtml), b);
            }
        }

        public int ParseInscriptions(int parentNodeId, HtmlNode htmlNode,
            ITextNodeWriter? writer = null)
        {
            if (htmlNode is null) throw new ArgumentNullException(nameof(htmlNode));
            ProgressReport? report = Progress != null
                ? new ProgressReport() : null;

            Logger?.LogInformation("[B] Inscriptions (#{ParentNodeId})",
                parentNodeId);
            int x = 0;
            List<TextNodeProperty> props = new();

            // each body/p (except last) is an inscription
            foreach (HtmlNode p in htmlNode.SelectNodes(
                "html/body/p[position() < last()]"))
            {
                // unwrap text from a/b as rarely a bold entry occurs inside a,
                // but this b would disrupt metadata splitting
                UnwrapBInA(p);

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

                // extract details if any (sometimes it is found inside p)
                if (html.IndexOf("<details", StringComparison.Ordinal) > -1)
                {
                    Match m = _detailsRegex.Match(html);
                    props.Add(new TextNodeProperty
                    {
                        NodeId = node.Id,
                        Name = "details",
                        Value = _wsRegex.Replace(m.Groups[1].Value, " ")
                    });
                    html = _detailsRegex.Replace(html, "");
                }

                // parse properties
                foreach (string part in html.Split("<br>").Select(s => s.Trim()))
                {
                    // text starts without tags
                    if (!part.StartsWith("<", StringComparison.Ordinal))
                    {
                        props.Add(new TextNodeProperty
                        {
                            NodeId = node.Id,
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
                                ParseMetadatum(node.Id,
                                    m.Groups[1].Value.Trim(),
                                    m.Groups[2].Value.Trim(),
                                    props);
                            }
                        }
                    }
                }

                // details after p
                if (p.NextSibling.Name == "details")
                {
                    props.Add(new TextNodeProperty
                    {
                        NodeId = node.Id,
                        Name = "details",
                        Value = _wsRegex.Replace(p.OuterHtml, " ")
                    });
                }

                // write node
                node.Name = props.Find(p => p.Name == "publication")?.Value ?? "";

                if (Progress != null)
                {
                    report!.Message = node.ToString();
                    Progress.Report(report);
                }

                writer?.Write(node, props.ToArray());

                props.Clear();
            }
            return x;
        }
    }
}
