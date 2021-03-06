using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Threading.Tasks;

namespace Epicod.Cli.Commands
{
    public sealed class RootCommand : ICommand
    {
        private readonly CommandLineApplication _app;

        public RootCommand(CommandLineApplication app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
        }

        public static void Configure(CommandLineApplication app, AppOptions options)
        {
            // configure all the app commands here
            app.Command("create-db", c => CreateDbCommand.Configure(c, options));
            app.Command("scrape-packhum", c => ScrapePackhumCommand.Configure(c, options));
            app.Command("inject-packhum", c => InjectPackhumPropsCommand.Configure(c, options));
            app.Command("dump-toc", c => DumpCorpusTocCommand.Configure(c, options));

            app.OnExecute(() =>
            {
                options.Command = new RootCommand(app);
                return 0;
            });
        }

        public Task Run()
        {
            _app.ShowHelp();
            return Task.FromResult(0);
        }
    }
}
