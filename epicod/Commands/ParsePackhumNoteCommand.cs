using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Epicod.Cli.Commands
{
    public sealed class ParsePackhumNoteCommand : ICommand
    {
        private readonly IConfiguration _config;
        private readonly string _dbName;
        private readonly bool _preflight;
        private readonly ILogger _logger;

        public ParsePackhumNoteCommand(AppOptions options, string dbName,
            bool preflight)
        {
            _config = options.Configuration;
            _dbName = dbName ?? "packhum";
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
                options.Command = new ParsePackhumNoteCommand(
                    options,
                    dbNameOption.Value(),
                    preflightOption.HasValue());
                return 0;
            });
        }

        public Task Run()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nPARSE PACKHUM NOTES\n");
            Console.ResetColor();
            Console.WriteLine(
                $"Database name: {_dbName}\n" +
                $"Preflight: {_preflight}\n");

            string connection = string.Format(CultureInfo.InvariantCulture,
                _config.GetConnectionString("Default"),
                _dbName);

            // TODO

            return Task.CompletedTask;
        }
    }
}
