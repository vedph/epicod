using Fusi.Api.Auth.Services;
using Fusi.DbManager;
using Fusi.DbManager.PgSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Epicod.Api.Models;
using System;
using System.IO;
using System.Threading;
using Epicod.Scraper.Sql;

namespace Epicod.Api.Services
{
    /// <summary>
    /// Application database initializer.
    /// </summary>
    public sealed class ApplicationDatabaseInitializer :
        AuthDatabaseInitializer<ApplicationUser, ApplicationRole, NamedSeededUserOptions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationDatabaseInitializer"/>
        /// class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        public ApplicationDatabaseInitializer(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        /// <summary>
        /// Initializes the user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="options">The options.</param>
        protected override void InitUser(ApplicationUser user,
            NamedSeededUserOptions options)
        {
            base.InitUser(user, options);

            user.FirstName = options.FirstName;
            user.LastName = options.LastName;
        }

        /// <summary>
        /// Initializes the database.
        /// </summary>
        protected override void InitDatabase()
        {
            string name = Configuration.GetValue<string>("DatabaseName");
            Serilog.Log.Information($"Checking for database {name}...");

            string csTemplate = Configuration.GetConnectionString("Default");
            PgSqlDbManager manager = new PgSqlDbManager(csTemplate);
            bool existing = manager.Exists(name);

            if (!existing)
            {
                Serilog.Log.Information($"Creating database {name}...");
                manager.CreateDatabase(name, EpicodSchema.Get(), null);
                Serilog.Log.Information("Database created.");

                // seed data from binary files if present
                string sourceDir = Configuration.GetValue<string>("Data:SourceDir");
                if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
                {
                    Logger?.LogInformation(
                        $"Data source directory {sourceDir} not found");
                    return;
                }

                Logger?.LogInformation("Seeding Epicod database from " + sourceDir);
                string cs = string.Format(csTemplate, name);
                BulkTablesCopier copier = new BulkTablesCopier(
                    new PgSqlBulkTableCopier(cs));
                copier.Begin();
                copier.Read(sourceDir, CancellationToken.None,
                    new Progress<string>((message) => Logger?.LogInformation(message)));
                copier.End();
                Logger?.LogInformation("Seeding completed.");
            }
        }
    }
}
