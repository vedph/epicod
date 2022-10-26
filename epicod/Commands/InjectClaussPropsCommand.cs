using Epicod.Cli.Services;
using Epicod.Scraper.Clauss;
using Fusi.Cli.Commands;
using Fusi.Tools;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using ShellProgressBar;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Epicod.Cli.Commands
{
    internal sealed class InjectClaussPropsCommand : ICommand
    {
        private readonly InjectClaussPropsCommandOptions _options;

        private InjectClaussPropsCommand(InjectClaussPropsCommandOptions options)
        {
            _options = options;
        }

        public static void Configure(CommandLineApplication app,
            ICliAppContext context)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));

            app.Description = "Parse note from Clauss into database";
            app.HelpOption("-?|-h|--help");

            CommandOption dbNameOption = app.Option("-d|--database",
                "Database name",
                CommandOptionType.SingleValue);

            CommandOption preflightOption = app.Option("-p|--preflight",
                "Preflight mode -- dont' write data to DB",
                CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                context.Command = new InjectClaussPropsCommand(
                    new InjectClaussPropsCommandOptions(context)
                    {
                        DatabaseName = dbNameOption.Value() ?? "epicod",
                        IsDry = preflightOption.HasValue()
                    });
                return 0;
            });
        }

        public Task Run()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nINJECT CLAUSS PROPERTIES\n");
            Console.ResetColor();
            Console.WriteLine(
                $"Database name: {_options.DatabaseName}\n" +
                $"Preflight: {(_options.IsDry ? "yes" : "no")}\n");

            string connection = string.Format(CultureInfo.InvariantCulture,
                _options.Configuration.GetConnectionString("Default"),
                _options.DatabaseName);

            ProgressBar bar = new(100, null, new ProgressBarOptions
            {
                // DisplayTimeInRealTime = false,
                EnableTaskBarProgress = true,
                CollapseWhenFinished = true
            });

            ClaussPropInjector injector = new(connection)
            {
                IsDry = _options.IsDry,
                Logger = _options.Logger
            };
            injector.Inject(CancellationToken.None,
                new Progress<ProgressReport>(report => bar.Tick(report.Percent)));

            return Task.CompletedTask;
        }
    }

    internal class InjectClaussPropsCommandOptions
        : CommandOptions<EpicodCliAppContext>
    {
        public InjectClaussPropsCommandOptions(ICliAppContext options)
            : base((EpicodCliAppContext)options)
        {
            DatabaseName = "epicod";
        }

        public string DatabaseName { get; set; }
        public bool IsDry { get; set; }
    }
}
