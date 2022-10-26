using Fusi.Tools;
using System;
using System.Threading;

namespace Epicod.Scraper
{
    public interface IPropInjector
    {
        bool IsDry { get; set; }

        int Inject(CancellationToken cancel,
            IProgress<ProgressReport>? progress = null);
    }
}
