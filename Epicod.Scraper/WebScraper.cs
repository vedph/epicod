using Fusi.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Epicod.Scraper
{
    /// <summary>
    /// Base class for web scrapers.
    /// </summary>
    public abstract class WebScraper
    {
        /// <summary>
        /// Gets the root URI.
        /// </summary>
        protected string? RootUri { get; private set; }

        /// <summary>
        /// Gets the cancellation token.
        /// </summary>
        protected CancellationToken Cancel { get; private set; }

        /// <summary>
        /// Gets the progress reporter.
        /// </summary>
        protected IProgress<ProgressReport>? Progress { get; private set; }

        /// <summary>
        /// Gets the progress report.
        /// </summary>
        protected ProgressReport? Report { get; private set; }

        /// <summary>
        /// Gets the writer used to write scraped nodes.
        /// </summary>
        protected ITextNodeWriter Writer { get; init; }

        /// <summary>
        /// Gets or sets the delay.
        /// </summary>
        public int Delay { get; set; }

        /// <summary>
        /// Gets or sets the timeout.
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the scraper should not
        /// write any data to its target.
        /// </summary>
        public bool IsDry { get; set; }

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
        /// Gets or sets the logger.
        /// </summary>
        public ILogger? Logger { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebScraper"/> class.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <exception cref="ArgumentNullException">writer</exception>
        protected WebScraper(ITextNodeWriter writer)
        {
            Writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        /// <summary>
        /// Does the scraping.
        /// </summary>
        protected abstract Task DoScrapeAsync();

        /// <summary>
        /// Scrapes the specified root URI.
        /// </summary>
        /// <param name="rootUri">The root URI.</param>
        /// <param name="cancel">The cancel.</param>
        /// <param name="progress">The progress.</param>
        /// <exception cref="ArgumentNullException">rootUrl</exception>
        public Task ScrapeAsync(string rootUri,
            CancellationToken cancel,
            IProgress<ProgressReport>? progress = null)
        {
            RootUri = rootUri ?? throw new ArgumentNullException(nameof(rootUri));
            Cancel = cancel;
            Progress = progress;
            Report = progress != null ? new ProgressReport() : null;

            return DoScrapeAsync();
        }
    }
}
