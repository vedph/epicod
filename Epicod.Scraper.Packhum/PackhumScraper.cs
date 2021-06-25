using Fusi.Tools;
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
using OpenQA.Selenium.Remote;

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
    public sealed class PackhumScraper
    {
        public const string CORPUS = "packhum";

        private readonly ITextNodeWriter _writer;
        private readonly PackhumNoteParser _parser;
        private string _rootUri;
        private CancellationToken _cancel;
        private IProgress<ProgressReport> _progress;
        private ProgressReport _report;
        private int _maxNodeId;
        private TextNode _currentNode;
        private RemoteWebDriver _driver;

        #region Properties
        /// <summary>
        /// Gets or sets the delay in milliseconds between text requests.
        /// </summary>
        public int Delay { get; set; }

        /// <summary>
        /// Gets or sets the page load timeout in seconds.
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// Gets or sets the Google Chrome path.
        /// </summary>
        public string ChromePath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this scraper is in dry
        /// run mode. When in this mode, no write to database occurs.
        /// </summary>
        public bool IsDry { get; set; }

        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this scraper should parse
        /// the text note.
        /// </summary>
        public bool IsNoteParsingEnabled { get; set; }
        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="PackhumScraper"/> class.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <exception cref="ArgumentNullException">writer</exception>
        public PackhumScraper(ITextNodeWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _parser = new PackhumNoteParser();
            Delay = 500;
            Timeout = 3 * 60;
        }

        // not thread-safe, we cannot parallelize anyway (give time to the server,
        // and keep exploring the branches in a reproducible way)
        private int GetNextNodeId() => ++_maxNodeId;

        private string GetAbsoluteHref(HtmlNode a)
        {
            string href = a.GetAttributeValue("href", null);
            Uri uri = new Uri(href, UriKind.RelativeOrAbsolute);
            return uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.ToAbsolute(_rootUri);
        }

        private void ReportProgressFor(TextNode node)
        {
            if (_progress == null) return;
            _report.Message = new string('-', node.Y - 1) + node.Name;
            _progress.Report(_report);
        }

        private ChromeDriver GetChromeDriver()
        {
            ChromeOptions options = new ChromeOptions
            {
                BinaryLocation = ChromePath
            };
            options.AddArguments(new List<string>() { "headless", "disable-gpu" });
            return new ChromeDriver(options);
        }

        // https://www.scrapingbee.com/blog/web-scraping-csharp/
        private string LoadDynamicPage(string uri,
            Func<ISearchContext, bool> isLoaded,
            RemoteWebDriver driver = null)
        {
            // create a default driver if none was specified
            if (driver == null)
            {
                if (_driver == null) _driver = GetChromeDriver();
                driver = _driver;
            }

            WebDriverWait wait = new WebDriverWait(driver,
                new TimeSpan(0, 0, Timeout));
            driver.Navigate().GoToUrl(uri);
            wait.Until(c => isLoaded(c));
            return driver.PageSource;
        }

        private void WriteNode(TextNode node,
            IList<TextNodeProperty> properties = null)
        {
            node.Corpus = CORPUS;
            Logger?.LogInformation(node.ToString());
            if (!IsDry) _writer.Write(node, properties);
        }

        #region Y=3 - texts
        private void ScrapeText(WebClient client, string uri, TextNode textNode)
        {
            string html = client.DownloadString(uri);
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            // lines from table[@class="grk"]/tbody/tr with 2 td for label and value
            StringBuilder text = new StringBuilder();

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
            string title = doc.DocumentNode
                .SelectSingleNode("//title")?.InnerText?.Trim();

            // info from span[@class="ti"]
            string note = doc.DocumentNode
                .SelectSingleNode("//span[@class=\"ti\"]")?.InnerText?.Trim();

            // PHI ID from //div[@class="docref"]/a
            string phi = doc.DocumentNode
                .SelectSingleNode("//div[@class=\"docref\"]/a")?.InnerText?.Trim();

            TextNode node = new TextNode
            {
                Id = GetNextNodeId(),
                ParentId = textNode.Id,
                Y = textNode.Y,
                X = textNode.X,
                Name = title,
                Uri = uri
            };

            // add text and metadata as node's properties
            List<TextNodeProperty> props = new List<TextNodeProperty>
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
                        var noteProps = _parser.Parse(note, node.Id);
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

        private string LoadDynamicTextsPage(string uri, RemoteWebDriver driver = null)
        {
            string html;
            try
            {
                html = LoadDynamicPage(uri, c =>
                {
                    try
                    {
                        // loaded when li@class contains item (range or not)
                        return c.FindElement(
                            By.XPath("//li[contains(@class, \"item\")]")) != null;
                    }
                    catch (NoSuchElementException) { return false; }
                }, driver);
            }
            catch (WebDriverTimeoutException)
            {
                Logger?.LogError("Timeout at " + uri);
                return null;
            }
            return html;
        }

        private void ScrapeSingleTextItems(WebClient client, HtmlDocument doc)
        {
            var nodes = doc.DocumentNode.SelectNodes("//li[@class=\"item\"]/a");
            if (nodes != null)
            {
                int x = 0;
                foreach (HtmlNode anchor in nodes)
                {
                    TextNode node = new TextNode
                    {
                        Id = GetNextNodeId(),
                        ParentId = _currentNode?.Id ?? 0,
                        Y = 3,
                        X = ++x,
                        Name = anchor.InnerText.Trim(),
                        Uri = GetAbsoluteHref(anchor)
                    };
                    WriteNode(node);

                    ReportProgressFor(node);

                    if (Delay > 0) Thread.Sleep(Delay);
                    ScrapeText(client, node.Uri, node);

                    if (_cancel.IsCancellationRequested) break;
                }
            }
        }

        private void ScrapeTexts(WebClient client, string uri)
        {
            Logger?.LogInformation("Texts at " + uri);

            string html = LoadDynamicTextsPage(uri);
            if (html == null) return;

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            // collect range items
            const string itemsPath = "//li[contains(@class, \"item\")]";
            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes(itemsPath);
            List<int> rangeIndexes = new List<int>();
            if (nodes != null)
            {
                int i = 0;
                foreach (HtmlNode li in nodes)
                {
                    if (li.HasClass("range")) rangeIndexes.Add(i);
                    i++;
                }
            }

            // single text items
            ScrapeSingleTextItems(client, doc);

            // range items
            if (rangeIndexes.Count > 0)
            {
                Logger?.LogInformation("Ranges to follow: " + rangeIndexes.Count);

                for (int i = 0; i < rangeIndexes.Count; i++)
                {
                    // click the li element and wait for load
                    IWebElement li = null;
                    try
                    {
                        int index = rangeIndexes[i];
                        Logger?.LogInformation($"Following range {i + 1}/" +
                            $"{rangeIndexes.Count} at {index}");

                        // reload the original texts list in the browser
                        Logger?.LogInformation("Reloading texts list from " + uri);
                        html = LoadDynamicTextsPage(uri);
                        if (html == null) return;

                        // get to the targeted li
                        li = _driver.FindElementByXPath(
                            $"({itemsPath})[{1 + index}]");
                        if (li == null)
                        {
                            Logger?.LogError($"Expected range li at {index} not found");
                            continue;
                        }

                        Logger?.LogInformation($"Clicking on range {li.Text}");
                        WebDriverWait wait = new WebDriverWait(_driver,
                            new TimeSpan(0, 0, Timeout));
                        // https://stackoverflow.com/questions/48665001/can-not-click-on-a-element-elementclickinterceptedexception-in-splinter-selen
                        _driver.ExecuteScript("arguments[0].click();", li);
                        // li.Click();

                        Logger?.LogInformation("Waiting for range target to load");
                        wait.Until(c =>
                        {
                            // loaded when li@class contains no range items
                            try
                            {
                                return c.FindElements(
                                    By.XPath("//li[contains(@class, \"range\")]"))
                                    .Count == 0;
                            }
                            catch (NoSuchElementException) { return true; }
                        });

                        // scrape texts (all single-text items)
                        HtmlDocument subDoc = new HtmlDocument();
                        subDoc.LoadHtml(_driver.PageSource);
                        ScrapeSingleTextItems(client, subDoc);
                    }
                    catch (WebDriverTimeoutException)
                    {
                        // timeout usually means that we have a rangeset
                        // inside a rangeset. Just log the error and continue,
                        // users will provide manual URIs here.
                        Logger?.LogError($"Timeout on following range {li?.Text} at {uri}");
                    }
                    catch (Exception ex)
                    {
                        // recover anyway from any other error
                        Logger?.LogError(ex.ToString());
                    }
                }
            }
        }
        #endregion

        #region Y=2 - books
        private void ScrapeBooks(WebClient client, string uri)
        {
            Logger?.LogInformation("Books at " + uri);

            string html = client.DownloadString(uri);
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            // books are grouped into div's each having ul/li/a books
            int x = 0;
            foreach (HtmlNode anchor in doc.DocumentNode.SelectNodes(
                "//div[@class=\"bookclass\"]//a"))
            {
                TextNode node = new TextNode
                {
                    Id = GetNextNodeId(),
                    ParentId = _currentNode?.Id ?? 0,
                    Y = 2,
                    X = ++x,
                    Name = anchor.InnerText.Trim(),
                    Uri = GetAbsoluteHref(anchor)
                };
                WriteNode(node);

                _currentNode = node;

                ReportProgressFor(node);

                try
                {
                    ScrapeTexts(client, node.Uri);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex.ToString());
                    Logger?.LogInformation("Resuming to next book");
                }

                if (_cancel.IsCancellationRequested) break;
            }
        }
        #endregion

        #region Y=1 - regions
        private void ScrapeRegions(WebClient client, string uri)
        {
            Logger?.LogInformation("Regions at " + uri);

            string html = client.DownloadString(uri);
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            // regions are in a table, each points to a book
            int x = 0;
            foreach (HtmlNode anchor in doc.DocumentNode.SelectNodes(
                "//table/tbody/tr/td/a"))
            {
                TextNode node = new TextNode
                {
                    Id = GetNextNodeId(),
                    ParentId = 0,
                    Y = 1,
                    X = ++x,
                    Name = anchor.InnerText.Trim(),
                    Uri = GetAbsoluteHref(anchor)
                };
                WriteNode(node);

                _currentNode = node;

                ReportProgressFor(node);

                ScrapeBooks(client, node.Uri);

                if (_cancel.IsCancellationRequested) break;
            }
        }
        #endregion

        /// <summary>
        /// Scrapes the specified root URI.
        /// </summary>
        /// <param name="rootUri">The root URI.</param>
        /// <param name="cancel">The cancel.</param>
        /// <param name="progress">The progress.</param>
        /// <exception cref="ArgumentNullException">rootUrl</exception>
        public void Scrape(string rootUri,
            CancellationToken cancel,
            IProgress<ProgressReport> progress = null)
        {
            if (rootUri == null) throw new ArgumentNullException(nameof(rootUri));

            _rootUri = rootUri;
            _cancel = cancel;
            _progress = progress;
            _report = progress != null ? new ProgressReport() : null;

            using (WebClient client = new WebClient())
            {
                ScrapeRegions(client, rootUri);
            }
        }
    }
}
