using Fusi.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace Epicod.Scraper
{
    /// <summary>
    /// A web scraper for Epicod data.
    /// </summary>
    public interface IWebScraper
    {
        /// <summary>
        /// Gets or sets the delay.
        /// </summary>
        int Delay { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the scraper should not
        /// write any data to its target.
        /// </summary>
        bool IsDry { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether note parsing is enabled
        /// while scraping.
        /// </summary>
        bool IsNoteParsingEnabled { get; set; }

        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        ILogger Logger { get; set; }

        /// <summary>
        /// Scrapes resources starting from the specified root URI.
        /// </summary>
        /// <param name="rootUri">The root URI.</param>
        /// <param name="cancel">The cancellation token.</param>
        /// <param name="progress">The optional progress reporter.</param>
        void Scrape(string rootUri, CancellationToken cancel,
            IProgress<ProgressReport> progress = null);
    }
}