using Epicod.Scraper.Packhum;
using Fusi.Tools;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShellProgressBar;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Epicod.Cli.Commands
{
    public sealed class InjectPackhumPropsCommand : ICommand
    {
        private readonly IConfiguration? _config;
        private readonly string? _dbName;
        private readonly bool _preflight;
        private readonly ILogger? _logger;

        public InjectPackhumPropsCommand(AppOptions options, string dbName,
            bool preflight)
        {
            _config = options.Configuration;
            _dbName = dbName ?? "epicod";
            _preflight = preflight;
            _logger = options.Logger;
        }

        public static void Configure(CommandLineApplication command,
            AppOptions options)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            command.Description = "Parse note from Packhum into database";
            command.HelpOption("-?|-h|--help");

            CommandOption dbNameOption = command.Option("-d|--database",
                "Database name",
                CommandOptionType.SingleValue);

            CommandOption preflightOption = command.Option("-p|--preflight",
                "Preflight mode -- dont' write data to DB",
                CommandOptionType.NoValue);

            command.OnExecute(() =>
            {
                options.Command = new InjectPackhumPropsCommand(
                    options,
                    dbNameOption.Value(),
                    preflightOption.HasValue());
                return 0;
            });
        }

        public Task Run()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nINJECT PACKHUM PROPERTIES\n");
            Console.ResetColor();
            Console.WriteLine(
                $"Database name: {_dbName}\n" +
                $"Preflight: {_preflight}\n");

            string connection = string.Format(CultureInfo.InvariantCulture,
                _config.GetConnectionString("Default"),
                _dbName);

            ProgressBar bar = new(100, null, new ProgressBarOptions
            {
                // DisplayTimeInRealTime = false,
                EnableTaskBarProgress = true,
                CollapseWhenFinished = true
            });

            PackhumPropInjector injector = new(connection);
            injector.Inject(CancellationToken.None,
                new Progress<ProgressReport>(report => bar.Tick(report.Percent)));

            return Task.CompletedTask;
        }
    }
}
