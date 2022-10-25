using Epicod.Cli.Services;
using Epicod.Sql;
using Fusi.Cli.Commands;
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
    internal sealed class DumpCorpusTocCommand : ICommand
    {
        private readonly DumpCorpusTocCommandOptions _options;

        public DumpCorpusTocCommand(DumpCorpusTocCommandOptions options)
        {
            _options = options;
        }

        public static void Configure(CommandLineApplication command,
            ICliAppContext context)
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
                context.Command = new DumpCorpusTocCommand(
                    new DumpCorpusTocCommandOptions(context)
                    {
                        DatabaseName = dbNameOption.Value(),
                        Corpus = corpusArgument.Value,
                        Properties = dbPropsOption.Values,
                        OutputPath = outputPathArgument.Value
                    });
                return 0;
            });
        }

        public Task Run()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nDUMP CORPUS TOC\n");
            Console.ResetColor();
            Console.WriteLine(
                $"Database name: {_options.DatabaseName}\n" +
                $"Corpus: {_options.Corpus}\n" +
                "Properties: " + string.Join(", ",
                    _options.Properties ?? Array.Empty<string>()) + "\n" +
                $"Output path: {_options.OutputPath}");

            string connection = string.Format(CultureInfo.InvariantCulture,
                _options.Configuration.GetConnectionString("Default"),
                _options.DatabaseName);

            using StreamWriter writer = new(_options.OutputPath, false, Encoding.UTF8);
            SqlCorpusTocDumper dumper = new(connection);
            dumper.Dump(_options.Corpus, _options.Properties, writer,
                CancellationToken.None,
                new Progress<int>(count =>
                {
                    Console.WriteLine(count);
                }));
            writer.Flush();

            return Task.CompletedTask;
        }
    }

    internal class DumpCorpusTocCommandOptions : CommandOptions<EpicodCliAppContext>
    {
        public string DatabaseName { get; set; }
        public string Corpus { get; set; }
        public IList<string>? Properties { get; set; }
        public string OutputPath { get; set; }

        public DumpCorpusTocCommandOptions(ICliAppContext options)
            : base((EpicodCliAppContext)options)
        {
            DatabaseName = "epicod";
            Corpus = "packhum";
            OutputPath = Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory);
        }
    }
}
