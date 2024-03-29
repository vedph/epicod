﻿using Fusi.Tools;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text;
using System.Threading;
using OpenQA.Selenium.Chrome;
using System.Collections.Generic;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using Epicod.Core;
using System.Threading.Tasks;
using Selenium.WebDriver.WaitExtensions;

// note: for Selenium you need the chrome driver from https://chromedriver.chromium.org/downloads
// here it was downloaded for version 91 and stored within the CLI project

namespace Epicod.Scraper.Packhum
{
    /// <summary>
    /// Web scraper for Packhard Humanities Greek inscriptions at
    /// inscriptions.packhum.org. This has a 3-levels hierarchy: regions,
    /// books, and texts. Each text has as a text property the inscription's
    /// text.
    /// </summary>
    public sealed class PackhumWebScraper : WebScraper, IWebScraper
    {
        public const string CORPUS = "packhum";
        private const string RANGE_ITEMS_PATH = "//li[contains(@class, \"range\")]";

        private readonly PackhumParser _parser;
        private readonly ChromeDriver _driver;
        private readonly List<int> _rangeSteps;
        private readonly HashSet<string> _consumedRangePaths;
        private int _maxTextX;

        /// <summary>
        /// Gets or sets the Google Chrome path.
        /// </summary>
        public string? ChromePath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether text leaves scraping is
        /// disabled. When this is true, the single text items links in the texts
        /// page are not followed. This can be used for diagnostic purposes,
        /// to speed up debugging when we are interested only in inspecting
        /// how the scraper behaves in walking the site tree.
        /// </summary>
        public bool IsTextLeafScrapingDisabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether note parsing is enabled
        /// while scraping.
        /// </summary>
        public bool IsNoteParsingEnabled { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PackhumWebScraper"/> class.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <exception cref="ArgumentNullException">writer</exception>
        public PackhumWebScraper(ITextNodeWriter writer) : base(writer)
        {
            _parser = new PackhumParser();
            _rangeSteps = new List<int>();
            _consumedRangePaths = new HashSet<string>();
            _driver = GetChromeDriver();
            Delay = 1500;
            Timeout = 3 * 60;
            Corpus = CORPUS;
        }

        private ChromeDriver GetChromeDriver()
        {
            ChromeOptions options = new()
            {
                BinaryLocation = ChromePath
            };
            options.AddArguments(new List<string>() { "headless", "disable-gpu" });
            return new ChromeDriver(options);
        }

        // https://www.scrapingbee.com/blog/web-scraping-csharp/
        private string LoadDynamicPage(string uri,
            Func<ISearchContext, bool> isLoaded)
        {
            WebDriverWait wait = new(_driver,
                new TimeSpan(0, 0, Timeout));
            _driver.Navigate().GoToUrl(uri);
            wait.Until(c => isLoaded(c));
            // additional wait time
            if (Delay > 0) Thread.Sleep(Delay);
            Logger?.LogInformation("Loaded page hash: " +
                _driver.PageSource.GetHashCode());
            return _driver.PageSource;
        }

        private string? LoadDynamicTextsPage(string uri)
        {
            try
            {
                // loaded when li@class contains unmarked item (range or not)
                return LoadDynamicPage(uri, c =>
                {
                    try
                    {
                        // loaded when li@class contains unmarked item (range or not)
                        return c.FindElement(
                            By.XPath("//li[contains(@class, \"item\") " +
                            "and not(@x)]")) != null;
                    }
                    catch (NoSuchElementException) { return false; }
                });
            }
            catch (WebDriverTimeoutException)
            {
                Logger?.LogError("Timeout at " + uri);
                return null;
            }
        }

        #region Y=3 - texts
        private void ScrapeText(WebClient client, string uri, TextNode node)
        {
            string html = client.DownloadString(uri);
            HtmlDocument doc = new();
            doc.LoadHtml(html);

            // lines from table[@class="grk"]/tbody/tr with 2 td for label and value
            StringBuilder text = new();

            foreach (HtmlNode trNode in doc.DocumentNode
                .SelectNodes("//table[@class=\"grk\"]/tr"))
            {
                int n = 0;
                foreach (HtmlNode td in trNode.Elements("td"))
                {
                    switch (++n)
                    {
                        case 1:
                            text.Append(td.InnerText.Trim()).Append('\t');
                            break;
                        case 2:
                            text.AppendLine(td.InnerText.Trim());
                            break;
                        default:
                            Logger?.LogWarning($"Unexpected td {n} with " +
                                $"\"{td.InnerText}\" at {uri}");
                            break;
                    }
                }
            }

            // title
            string? title = doc.DocumentNode
                .SelectSingleNode("//title")?.InnerText?.Trim();
            if (!string.IsNullOrEmpty(title)) node.Name = title;

            // info from span[@class="ti"]
            string? note = doc.DocumentNode
                .SelectSingleNode("//span[@class=\"ti\"]")?.InnerText?.Trim();

            // PHI ID from //div[@class="docref"]/a
            string? phi = doc.DocumentNode
                .SelectSingleNode("//div[@class=\"docref\"]/a")?.InnerText?.Trim();

            // add text and metadata as node's properties
            List<TextNodeProperty> props = new()
            {
                new TextNodeProperty(node.Id, "text", text.ToString())
            };
            if (!string.IsNullOrEmpty(note))
            {
                // resolve &amp; into &
                note = note.Replace("&amp;", "&");

                props.Add(new TextNodeProperty(node.Id, "note", note));
                if (IsNoteParsingEnabled)
                {
                    try
                    {
                        var noteProps = _parser.ParseNote(note, node.Id);
                        props.AddRange(noteProps);
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex.ToString());
                    }
                }
            }
            if (!string.IsNullOrEmpty(phi))
                props.Add(new TextNodeProperty(node.Id, "phi", phi));

            WriteNode(node, props);
        }

        private void SetAttributeValue(IWebElement element, string name, string value)
            => _driver.ExecuteScript("arguments[0].setAttribute" +
                "(arguments[1], arguments[2]);", element, name, value);

        private void MarkAllItemNodes()
        {
            // mark all the item/range li elements
            var elements = _driver.FindElements(By.ClassName("item"));
            if (elements == null) return;

            foreach (IWebElement element in elements)
                SetAttributeValue(element, "x", "1");
        }

        private void LoadTextPageFromPath(string uri, IList<int> indexes)
        {
            Logger?.LogInformation("Repositioning to /" +
                string.Join("/", indexes) +
                " starting from " + uri);

            // load the root page from uri
            Logger?.LogInformation("Loading page from " + uri);
            if (LoadDynamicTextsPage(uri) == null) return;

            // walk the path
            foreach (int i in indexes)
            {
                // mark all the item/range li elements with @x=1
                MarkAllItemNodes();

                // click the li element corresponding to this path step.
                // This triggers a new AJAX load
                IWebElement li = _driver.FindElement(By.XPath(
                    $"({RANGE_ITEMS_PATH})[{1 + i}]"));
                if (li == null)
                {
                    string error =
                        $"Expected {i + 1}nth range item li not found in {uri}";
                    Logger?.LogError(error);
                    throw new InvalidOperationException(error);
                }
                Logger?.LogInformation("Walking via " + li.Text);
                _driver.ExecuteScript("arguments[0].click();", li);

                // wait until there are new li items again (the li items
                // existing before clicking are all marked with @x)
                _driver.Wait(5000).ForElement(
                    By.XPath("//li[contains(@class, \"item\") and not(@x)]"));
                // a new non-@x item is not enough, let the page load all of them
                // _driver.Wait(Delay)
                if (Delay > 0) Thread.Sleep(Delay);
                Logger?.LogInformation("Loaded page hash: " +
                    _driver.PageSource.GetHashCode());
            }
            MarkAllItemNodes();

            Logger?.LogInformation("Repositioning completed");
        }

        private void ScrapeSingleTextItems(WebClient client, HtmlDocument doc,
            TextNode parentNode)
        {
            var nodes = doc.DocumentNode.SelectNodes("//li[@class=\"item\"]/a");

            if (nodes != null)
            {
                Logger?.LogInformation("Text items: " + nodes.Count);
                if (IsTextLeafScrapingDisabled) return;

                foreach (HtmlNode anchor in nodes)
                {
                    TextNode node = new()
                    {
                        Id = GetNextNodeId(),
                        ParentId = parentNode.Id,
                        Y = 3,
                        X = ++_maxTextX,
                        Name = anchor.InnerText.Trim(),
                        Uri = GetAbsoluteHref(anchor)
                    };
                    // if (Delay > 0) Thread.Sleep(Delay)
                    ScrapeText(client, node.Uri!, node);
                    ReportProgressFor(node);
                    if (Cancel.IsCancellationRequested) break;
                }
            }
        }

        private string GetCurrentPath() => string.Join("/", _rangeSteps);

        private static IList<int> CollectRangeItemIndexes(HtmlDocument doc)
        {
            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes(RANGE_ITEMS_PATH);
            List<int> indexes = new();
            if (nodes != null)
            {
                int i = 0;
                foreach (HtmlNode li in nodes)
                {
                    if (li.HasClass("range")) indexes.Add(i);
                    i++;
                }
            }
            return indexes;
        }

        private void ScrapeTexts(WebClient client, string uri, TextNode parentNode,
            string? html = null)
        {
            // load page unless HTML provided
            string path = GetCurrentPath();
            Logger?.LogInformation(
                $"[C] Texts at {uri}{(html != null ? "*" : "")}: path /{path}");

            if (html == null)
            {
                Logger?.LogInformation("Loading page from " + uri);
                if (LoadDynamicTextsPage(uri) == null) return;
                // mark all the item nodes with @x=1
                MarkAllItemNodes();
                html = _driver.PageSource;
            }

            // parse HTML
            HtmlDocument doc = new();
            doc.LoadHtml(html);

            // collect range items (li @class="range item"), marking them with @x=1
            IList<int> rangeIndexes = CollectRangeItemIndexes(doc);

            // follow all the single text items if not already done
            // (_driver is not used, so it remains on the current page)
            if (!_consumedRangePaths.Contains(path))
            {
                ScrapeSingleTextItems(client, doc, parentNode);
                _consumedRangePaths.Add(path);
            }

            // process the collected range items if any
            if (rangeIndexes.Count == 0) return;
            Logger?.LogInformation("Ranges to follow: " + rangeIndexes.Count);

            for (int i = 0; i < rangeIndexes.Count; i++)
            {
                // push index on steps
                _rangeSteps.Add(i);

                // update page by clicking on range item li
                IWebElement li = _driver.FindElement(By.XPath(
                    $"({RANGE_ITEMS_PATH})[{1 + i}]"));
                if (li == null)
                {
                    string error = $"Expected {i+1}nth range item li not found in {uri}";
                    Logger?.LogError(error);
                    throw new InvalidOperationException(error);
                }
                Logger?.LogInformation($"Range {i + 1}/{rangeIndexes.Count}: " +
                    GetCurrentPath() + $": \"{li.Text}\"");
                _driver.ExecuteScript("arguments[0].click();", li);

                // wait until there are new li items again (the li items existing
                // before clicking are all marked with @x)
                _driver.Wait(5000).ForElement(
                    By.XPath("//li[contains(@class, \"item\") and not(@x)]"));
                // a new non-@x item is not enough, let the page load all of them
                if (Delay > 0) Thread.Sleep(Delay);
                // _driver.Wait(Delay)

                Logger?.LogInformation("Loaded page hash: " +
                    _driver.PageSource.GetHashCode());

                // scrape the newly loaded page passing its already loaded HTML
                MarkAllItemNodes();
                ScrapeTexts(client, uri, parentNode, _driver.PageSource);

                // pop index from steps
                _rangeSteps.RemoveAt(_rangeSteps.Count - 1);

                // if we're continuing the loop, reload from the root texts page,
                // marking items in it with @x
                if (i + 1 < rangeIndexes.Count)
                    LoadTextPageFromPath(uri, _rangeSteps);
            }
        }
        #endregion

        #region Y=2 - books
        private void ScrapeBooks(WebClient client, string uri, TextNode parentNode)
        {
            Logger?.LogInformation("[B] Books at " + uri);

            string html = client.DownloadString(uri);
            HtmlDocument doc = new();
            doc.LoadHtml(html);

            // books are grouped into div's each having ul/li/a books
            int x = 0;
            foreach (HtmlNode anchor in doc.DocumentNode.SelectNodes(
                "//div[@class=\"bookclass\"]//a"))
            {
                TextNode node = new()
                {
                    Id = GetNextNodeId(),
                    ParentId = parentNode.Id,
                    Y = 2,
                    X = ++x,
                    Name = anchor.InnerText.Trim(),
                    Uri = GetAbsoluteHref(anchor)
                };
                WriteNode(node);
                ReportProgressFor(node);

                try
                {
                    // we're entering the troublesome page here, because
                    // the node URI will be a page dynamically updated via AJAX
                    _rangeSteps.Clear();
                    _consumedRangePaths.Clear();
                    _maxTextX = 0;
                    ScrapeTexts(client, node.Uri!, node, null);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex.ToString());
                    Logger?.LogInformation("Resuming to next book");
                }

                if (Cancel.IsCancellationRequested) break;
            }
        }
        #endregion

        #region Y=1 - regions
        private void ScrapeRegions(WebClient client, string uri)
        {
            Logger?.LogInformation("[A] Regions at " + uri);

            string html = client.DownloadString(uri);
            HtmlDocument doc = new();
            doc.LoadHtml(html);

            // regions are in a table, each points to a book
            int x = 0;
            foreach (HtmlNode anchor in doc.DocumentNode.SelectNodes(
                "//table/tbody/tr/td/a"))
            {
                TextNode node = new()
                {
                    Id = GetNextNodeId(),
                    ParentId = 0,
                    Y = 1,
                    X = ++x,
                    Name = anchor.InnerText.Trim(),
                    Uri = GetAbsoluteHref(anchor)
                };
                WriteNode(node);
                ReportProgressFor(node);

                ScrapeBooks(client, node.Uri!, node);

                if (Cancel.IsCancellationRequested) break;
            }
        }
        #endregion

        /// <summary>
        /// Scrapes the specified root URI.
        /// </summary>
        /// <exception cref="ArgumentNullException">rootUrl</exception>
        protected override Task DoScrapeAsync()
        {
            using (WebClient client = new())
            {
                ScrapeRegions(client, RootUri!);
            }
            return Task.CompletedTask;
        }
    }
}
