using Fusi.Tools;
using System;
using System.Threading;

namespace Epicod.Scraper
{
    public interface IPropInjector
    {
        int Inject(CancellationToken cancel,
            IProgress<ProgressReport>? progress = null);
    }
}
