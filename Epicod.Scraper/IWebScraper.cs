﻿using Fusi.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

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
        /// Gets or sets the timeout.
        /// </summary>
        int Timeout { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the scraper should not
        /// write any data to its target.
        /// </summary>
        bool IsDry { get; set; }

        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        ILogger? Logger { get; set; }

        /// <summary>
        /// Scrapes resources starting from the specified root URI.
        /// </summary>
        /// <param name="rootUri">The root URI.</param>
        /// <param name="cancel">The cancellation token.</param>
        /// <param name="progress">The optional progress reporter.</param>
        /// <param name="baseNodeId">The base node ID.</param>
        Task ScrapeAsync(string rootUri, CancellationToken cancel,
            IProgress<ProgressReport>? progress = null,
            int baseNodeId = 0);
    }
}