using Epicod.Sql;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Epicod.Cli.Commands
{
    public sealed class DumpCorpusTocCommand : ICommand
    {
        private readonly IConfiguration _config;
        private readonly string _dbName;
        private readonly string _corpus;
        private readonly IList<string> _properties;
        private readonly string _outputPath;

        public DumpCorpusTocCommand(AppOptions options, string dbName,
            string corpus, IList<string> properties, string outputPath)
        {
            _config = options.Configuration;
            _dbName = dbName ?? "epicod";
            _corpus = corpus;
            _properties = properties;
            _outputPath = outputPath;
        }

        public static void Configure(CommandLineApplication command,
            AppOptions options)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            command.Description = "Dump corpus TOC to TSV file";
            command.HelpOption("-?|-h|--help");

            CommandArgument corpusArgument = command.Argument("corpus",
                "The corpus name");

            CommandArgument outputPathArgument = command.Argument("output",
                "The output path");

            CommandOption dbNameOption = command.Option("-d|--database",
                "Database name",
                CommandOptionType.SingleValue);

            CommandOption dbPropsOption = command.Option("-p|--property",
                "Name of node property to be dumped",
                CommandOptionType.MultipleValue);

            command.OnExecute(() =>
            {
                options.Command = new DumpCorpusTocCommand(
                    options,
                    dbNameOption.Value(),
                    corpusArgument.Value,
                    dbPropsOption.Values,
                    outputPathArgument.Value);
                return 0;
            });
        }

        public Task Run()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nDUMP CORPUS TOC\n");
            Console.ResetColor();
            Console.WriteLine(
                $"Database name: {_dbName}\n" +
                $"Corpus: {_corpus}\n" +
                "Properties: " + string.Join(", ",
                    _properties ?? Array.Empty<string>()) + "\n" +
                $"Output path: {_outputPath}");

            string connection = string.Format(CultureInfo.InvariantCulture,
                _config.GetConnectionString("Default"),
                _dbName);

            using StreamWriter writer = new StreamWriter(_outputPath, false,
                Encoding.UTF8);
            SqlCorpusTocDumper dumper = new SqlCorpusTocDumper(connection);
            dumper.Dump(_corpus, _properties, writer,
                CancellationToken.None,
                new Progress<int>(count =>
                {
                    Console.WriteLine(count);
                }));
            writer.Flush();

            return Task.CompletedTask;
        }
    }
}
