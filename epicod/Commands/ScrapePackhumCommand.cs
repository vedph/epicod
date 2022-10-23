using Epicod.Scraper.Packhum;
using Epicod.Scraper.Sql;
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
    public sealed class ScrapePackhumCommand : ICommand
    {
        private readonly IConfiguration? _config;
        private readonly ILogger? _logger;
        private readonly ScrapePackhumCommandOptions _options;

        public ScrapePackhumCommand(ScrapePackhumCommandOptions options)
        {
            _config = options.AppOptions?.Configuration;
            _logger = options.AppOptions?.Logger;
            _options = options;
        }

        public static void Configure(CommandLineApplication command,
            AppOptions options)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            command.Description = "Scrape Packhum into database";
            command.HelpOption("-?|-h|--help");

            CommandOption dbNameOption = command.Option("-d|--database",
                "Database name",
                CommandOptionType.SingleValue);

            CommandOption preflightOption = command.Option("-p|--preflight",
                "Preflight mode -- dont' write data to DB",
                CommandOptionType.NoValue);

            CommandOption noTextOption = command.Option("-x|--no-text",
                "No texts -- don't follow single text items links",
                CommandOptionType.NoValue);

            CommandOption delayOption = command.Option("-l|--delay",
                "The delay between text requests in milliseconds (1500ms)",
                CommandOptionType.SingleValue);

            CommandOption timeoutOption = command.Option("-t|--timeout",
                "The texts page load timeout in seconds (120s)",
                CommandOptionType.SingleValue);

            CommandOption noteParsingOption = command.Option("-n|--note",
                "Enable text note parsing",
                CommandOptionType.NoValue);

            command.OnExecute(() =>
            {
                int delay = (delayOption.HasValue()
                    && int.TryParse(delayOption.Value(), out int d))
                    ? d : 1500;
                int timeout = (timeoutOption.HasValue()
                    && int.TryParse(timeoutOption.Value(), out int t))
                    ? t : 2 * 60;

                options.Command = new ScrapePackhumCommand(
                    new ScrapePackhumCommandOptions
                    {
                        AppOptions = options,
                        DatabaseName = dbNameOption.Value() ?? "epicod",
                        IsDry = preflightOption.HasValue(),
                        IsTextLeafScrapingDisabled = noTextOption.HasValue(),
                        Delay = delay,
                        Timeout = timeout,
                        IsNoteParsingEnabled = noteParsingOption.HasValue()
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
                $"Note parsing: {(_options.IsNoteParsingEnabled? "yes":"no")}\n");

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
                _config.GetConnectionString("Default"),
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

            PackhumWebScraper scraper = new(new SqlTextNodeWriter(connection))
            {
                ChromePath = _config!.GetSection("Selenium")
                    .GetSection("ChromePath-" + OsHelper.GetCode()).Value,
                Logger = _logger,
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
                    new Progress<ProgressReport>(r => Console.WriteLine(r.Message)));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.ToString());
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.ToString());
                Console.ResetColor();
            }
        }
    }

    public class ScrapePackhumCommandOptions
    {
        public AppOptions? AppOptions { get; set; }
        public string? DatabaseName { get; set; }
        public bool IsDry { get; set; }
        public bool IsTextLeafScrapingDisabled { get; set; }
        public int Delay { get; set; }
        public int Timeout { get; set; }
        public bool IsNoteParsingEnabled { get; set; }
    }
}
