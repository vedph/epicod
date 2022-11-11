using Epicod.Cli.Services;
using Epicod.Scraper.Sql;
using Fusi.Cli.Commands;
using Fusi.DbManager;
using Fusi.DbManager.PgSql;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace Epicod.Cli.Commands
{
    internal sealed class CreateDbCommand : ICommand
    {
        private readonly CreateDbCommandOptions _options;

        private CreateDbCommand(CreateDbCommandOptions options)
        {
            _options = options;
        }

        public static void Configure(CommandLineApplication app,
            ICliAppContext context)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));

            app.Description = "Create an empty scraper database with its schema";
            app.HelpOption("-?|-h|--help");

            CommandArgument dbArgument = app.Argument("[database]",
                "The name of the database to create");

            app.OnExecute(() =>
            {
                context.Command = new CreateDbCommand(
                    new CreateDbCommandOptions(context)
                    {
                        DatabaseName = dbArgument.Value ?? "epicod"
                    });
                return 0;
            });
        }

        public Task Run()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nCREATE SCRAPER DATABASE\n");
            Console.ResetColor();
            Console.WriteLine($"Database name: {_options.DatabaseName}\n");

            // create database if not exists
            IDbManager manager = new PgSqlDbManager(
                _options.Configuration!.GetConnectionString("Default")!);
            if (manager.Exists(_options.DatabaseName))
            {
                Console.Write($"Database {_options.DatabaseName} already exists");
                return Task.CompletedTask;
            }

            Console.Write($"Creating {_options.DatabaseName}...");
            manager.CreateDatabase(_options.DatabaseName!,
                EpicodSchema.Get(), null);
            Console.WriteLine(" done");

            return Task.CompletedTask;
        }
    }

    internal class CreateDbCommandOptions : CommandOptions<EpicodCliAppContext>
    {
        public CreateDbCommandOptions(ICliAppContext context) :
            base((EpicodCliAppContext)context)
        {
            DatabaseName = "epicod";
        }

        public string DatabaseName { get; set; }
    }
}