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
        private readonly IConfiguration _config;
        private readonly string _dbName;
        private readonly bool _preflight;
        private readonly int _delay;
        private readonly int _timeout;
        private readonly bool _noteParsing;
        private readonly ILogger _logger;

        public ScrapePackhumCommand(AppOptions options, string dbName,
            bool preflight, int delay, int timeout, bool noteParsing)
        {
            _config = options.Configuration;
            _dbName = dbName ?? "packhum";
            _preflight = preflight;
            _delay = delay;
            _timeout = timeout;
            _logger = options.Logger;
            _noteParsing = noteParsing;
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

            CommandOption delayOption = command.Option("-l|--delay",
                "The delay between text requests in milliseconds",
                CommandOptionType.SingleValue);

            CommandOption timeoutOption = command.Option("-t|--timeout",
                "The texts page load timeout in seconds",
                CommandOptionType.SingleValue);

            CommandOption noteParsingOption = command.Option("-n|--note",
                "Enable text note parsing",
                CommandOptionType.NoValue);

            command.OnExecute(() =>
            {
                int delay = (delayOption.HasValue()
                    && int.TryParse(delayOption.Value(), out int d))
                    ? d : 500;
                int timeout = (timeoutOption.HasValue()
                    && int.TryParse(timeoutOption.Value(), out int t))
                    ? t : 3 * 60;

                options.Command = new ScrapePackhumCommand(
                    options,
                    dbNameOption.Value(),
                    preflightOption.HasValue(),
                    delay,
                    timeout,
                    noteParsingOption.HasValue());
                return 0;
            });
        }

        public Task Run()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nSCRAPE PACKHUM\n");
            Console.ResetColor();
            Console.WriteLine(
                $"Database name: {_dbName}\n" +
                $"Preflight: {_preflight}\n" +
                $"Delay: {_delay}\n" +
                $"Timeout: {_timeout}\n" +
                $"Note parsing: {(_noteParsing? "yes":"no")}");

            // check that Selenium driver for Chrome is present
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "chromedriver.exe");
            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The Selenium Chrome driver is expected at " +
                    path);
                Console.ResetColor();
                Console.WriteLine("You can get it from https://chromedriver.chromium.org/downloads");
                return Task.CompletedTask;
            }

            // create database if not exists
            string connection = string.Format(CultureInfo.InvariantCulture,
                _config.GetConnectionString("Default"),
                _dbName);

            if (!_preflight)
            {
                IDbManager manager = new PgSqlDbManager(connection);
                if (manager.Exists(_dbName))
                {
                    Console.Write($"Clearing {_dbName}...");
                    manager.ClearDatabase(_dbName);
                    Console.WriteLine(" done");
                }
                else
                {
                    Console.Write($"Creating {_dbName}...");
                    manager.CreateDatabase(_dbName,
                        ScraperDbSchema.Get(), null);
                    Console.WriteLine(" done");
                }
            }

            PackhumScraper scraper = new PackhumScraper(
                new SqlTextNodeWriter(connection))
            {
                ChromePath = _config.GetSection("Selenium").GetSection("ChromePath").Value,
                Logger = _logger,
                Delay = _delay,
                Timeout = _timeout,
                IsDry = _preflight
            };
            try
            {
                scraper.Scrape("https://inscriptions.packhum.org/allregions",
                    CancellationToken.None,
                    new Progress<ProgressReport>(r =>
                    {
                        Console.WriteLine(r.Message);
                    }));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.ToString());
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.ToString());
                Console.ResetColor();
            }

            return Task.CompletedTask;
        }
    }
}
