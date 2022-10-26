using Epicod.Cli.Services;
using Epicod.Scraper.Clauss;
using Epicod.Scraper.Sql;
using Fusi.Cli.Commands;
using Fusi.DbManager;
using Fusi.DbManager.PgSql;
using Fusi.Tools;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Epicod.Cli.Commands
{
    internal sealed class ScrapeClaussCommand : ICommand
    {
        private readonly ScrapeClaussCommandOptions _options;

        public ScrapeClaussCommand(ScrapeClaussCommandOptions options)
        {
            _options = options;
        }

        public static void Configure(CommandLineApplication app,
            ICliAppContext context)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));

            app.Description = "Scrape Clauss into database";
            app.HelpOption("-?|-h|--help");

            CommandOption dbNameOption = app.Option("-d|--database",
                "Database name",
                CommandOptionType.SingleValue);

            CommandOption preflightOption = app.Option("-p|--preflight",
                "Preflight mode -- dont' write data to DB",
                CommandOptionType.NoValue);

            CommandOption delayOption = app.Option("-l|--delay",
                "The delay between text requests in milliseconds (1500ms)",
                CommandOptionType.SingleValue);

            CommandOption timeoutOption = app.Option("-t|--timeout",
                "The texts page load timeout in seconds (120s)",
                CommandOptionType.SingleValue);

            CommandOption baseNodeIdOption = app.Option("-i|--id",
                "Set the base node ID value (default=0)",
                CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                int delay = (delayOption.HasValue()
                    && int.TryParse(delayOption.Value(), out int d))
                    ? d : 1500;
                int timeout = (timeoutOption.HasValue()
                    && int.TryParse(timeoutOption.Value(), out int t))
                    ? t : 2 * 60;

                context.Command = new ScrapeClaussCommand(
                    new ScrapeClaussCommandOptions(context)
                    {
                        DatabaseName = dbNameOption.Value() ?? "epicod",
                        IsDry = preflightOption.HasValue(),
                        Delay = delay,
                        Timeout = timeout,
                        BaseNodeId = baseNodeIdOption.HasValue()
                            ? int.Parse(baseNodeIdOption.Value(),
                                CultureInfo.InvariantCulture)
                            : 0
                    });
                return 0;
            });
        }

        public async Task Run()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nSCRAPE CLAUSS\n");
            Console.ResetColor();
            Console.WriteLine(
                $"Database name: {_options.DatabaseName}\n" +
                $"Preflight: {_options.IsDry}\n" +
                $"Delay: {_options.Delay}\n" +
                $"Timeout: {_options.Timeout}\n");

            // create database if not exists
            string connection = string.Format(CultureInfo.InvariantCulture,
                _options.Context.Configuration!.GetConnectionString("Default"),
                _options.DatabaseName);

            if (!_options.IsDry)
            {
                IDbManager manager = new PgSqlDbManager(connection);
                if (manager.Exists(_options.DatabaseName!))
                {
                    Console.Write($"Clearing {_options.DatabaseName}...");
                    manager.ClearDatabase(_options.DatabaseName!);
                    Console.WriteLine(" done");
                }
                else
                {
                    Console.Write($"Creating {_options.DatabaseName}...");
                    manager.CreateDatabase(_options.DatabaseName!,
                        EpicodSchema.Get(), null);
                    Console.WriteLine(" done");
                }
            }

            ClaussWebScraper scraper = new(new SqlTextNodeWriter(connection))
            {
                Logger = _options.Logger,
                Delay = _options.Delay,
                Timeout = _options.Timeout,
                IsDry = _options.IsDry
            };
            try
            {
                await scraper.ScrapeAsync("https://db.edcs.eu/epigr/epitest.php",
                    CancellationToken.None,
                    new Progress<ProgressReport>(r => Console.WriteLine(r.Message)));
            }
            catch (Exception ex)
            {
                _options.Logger?.LogError(ex.ToString());
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.ToString());
                Console.ResetColor();
            }
        }
    }

    internal class ScrapeClaussCommandOptions :
        CommandOptions<EpicodCliAppContext>
    {
        public ScrapeClaussCommandOptions(ICliAppContext options)
            : base((EpicodCliAppContext)options)
        {
        }

        public string? DatabaseName { get; set; }
        public bool IsDry { get; set; }
        public int Delay { get; set; }
        public int Timeout { get; set; }
        public int BaseNodeId { get; set; }
    }
}
