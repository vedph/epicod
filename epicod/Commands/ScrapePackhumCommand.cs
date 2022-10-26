using Epicod.Cli.Services;
using Epicod.Scraper.Packhum;
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Epicod.Cli.Commands
{
    internal sealed class ScrapePackhumCommand : ICommand
    {
        private readonly ScrapePackhumCommandOptions _options;

        private ScrapePackhumCommand(ScrapePackhumCommandOptions options)
        {
            _options = options;
        }

        public static void Configure(CommandLineApplication app,
            ICliAppContext context)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));

            app.Description = "Scrape Packhum into database";
            app.HelpOption("-?|-h|--help");

            CommandOption dbNameOption = app.Option("-d|--database",
                "Database name",
                CommandOptionType.SingleValue);

            CommandOption preflightOption = app.Option("-p|--preflight",
                "Preflight mode -- dont' write data to DB",
                CommandOptionType.NoValue);

            CommandOption clearOption = app.Option("-c|--clear",
                "Clear the target database before scraping",
                CommandOptionType.NoValue);

            CommandOption noTextOption = app.Option("-x|--no-text",
                "No texts -- don't follow single text items links",
                CommandOptionType.NoValue);

            CommandOption delayOption = app.Option("-l|--delay",
                "The delay between text requests in milliseconds (1500ms)",
                CommandOptionType.SingleValue);

            CommandOption timeoutOption = app.Option("-t|--timeout",
                "The texts page load timeout in seconds (120s)",
                CommandOptionType.SingleValue);

            CommandOption noteParsingOption = app.Option("-n|--note",
                "Enable text note parsing",
                CommandOptionType.NoValue);

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

                context.Command = new ScrapePackhumCommand(
                    new ScrapePackhumCommandOptions(context)
                    {
                        DatabaseName = dbNameOption.Value() ?? "epicod",
                        IsDry = preflightOption.HasValue(),
                        IsClearEnabled = clearOption.HasValue(),
                        IsTextLeafScrapingDisabled = noTextOption.HasValue(),
                        Delay = delay,
                        Timeout = timeout,
                        IsNoteParsingEnabled = noteParsingOption.HasValue(),
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
            Console.WriteLine("\nSCRAPE PACKHUM\n");
            Console.ResetColor();
            Console.WriteLine(
                $"Database name: {_options.DatabaseName}\n" +
                $"Preflight: {_options.IsDry}\n" +
                $"Delay: {_options.Delay}\n" +
                $"Timeout: {_options.Timeout}\n" +
                $"Note parsing: {(_options.IsNoteParsingEnabled? "yes":"no")}\n" +
                $"Base node ID: {_options.BaseNodeId}\n");

            // check that Selenium driver for Chrome is present
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                OsHelper.IsWindows()? "chromedriver.exe" : "chromedriver");
            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The Selenium Chrome driver is expected at " +
                    path);
                Console.ResetColor();
                Console.WriteLine("You can get it from https://chromedriver.chromium.org/downloads");
                return;
            }

            // create database if not exists
            string connection = string.Format(CultureInfo.InvariantCulture,
                _options.Context.Configuration!.GetConnectionString("Default"),
                _options.DatabaseName);

            if (!_options.IsDry)
            {
                IDbManager manager = new PgSqlDbManager(connection);
                if (manager.Exists(_options.DatabaseName!))
                {
                    if (_options.IsClearEnabled)
                    {
                        Console.Write($"Clearing {_options.DatabaseName}...");
                        manager.ClearDatabase(_options.DatabaseName!);
                        Console.WriteLine(" done");
                    }
                }
                else
                {
                    Console.Write($"Creating {_options.DatabaseName}...");
                    manager.CreateDatabase(_options.DatabaseName!,
                        EpicodSchema.Get(), null);
                    Console.WriteLine(" done");
                }
            }

            PackhumWebScraper scraper = new(new SqlTextNodeWriter(connection))
            {
                ChromePath = _options.Configuration!.GetSection("Selenium")
                    .GetSection("ChromePath-" + OsHelper.GetCode()).Value,
                Logger = _options.Logger,
                Delay = _options.Delay,
                Timeout = _options.Timeout,
                IsDry = _options.IsDry,
                IsTextLeafScrapingDisabled = _options.IsTextLeafScrapingDisabled,
                IsNoteParsingEnabled = _options.IsNoteParsingEnabled
            };
            try
            {
                await scraper.ScrapeAsync("https://inscriptions.packhum.org/allregions",
                    CancellationToken.None,
                    new Progress<ProgressReport>(r => Console.WriteLine(r.Message)),
                    _options.BaseNodeId - 1);
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

    internal class ScrapePackhumCommandOptions :
        CommandOptions<EpicodCliAppContext>
    {
        public ScrapePackhumCommandOptions(ICliAppContext options)
            : base((EpicodCliAppContext)options)
        {
        }

        public string? DatabaseName { get; set; }
        public bool IsDry { get; set; }
        public bool IsClearEnabled { get; set; }
        public bool IsTextLeafScrapingDisabled { get; set; }
        public int Delay { get; set; }
        public int Timeout { get; set; }
        public bool IsNoteParsingEnabled { get; set; }
        public int BaseNodeId { get; set; }
    }
}
