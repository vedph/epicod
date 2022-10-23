using Fusi.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Epicod.Scraper.Clauss
{
    public sealed class ClaussWebScraper : IWebScraper
    {
        public int Delay { get; set; }
        public int Timeout { get; set; }
        public bool IsDry { get; set; }
        public bool IsTextLeafScrapingDisabled { get; set; }
        public bool IsNoteParsingEnabled { get; set; }
        public ILogger? Logger { get; set; }

        public Task ScrapeAsync(string rootUri, CancellationToken cancel,
            IProgress<ProgressReport>? progress = null)
        {
            throw new NotImplementedException();
        }
    }
}
