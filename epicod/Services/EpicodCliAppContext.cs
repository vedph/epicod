using Fusi.Cli.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.IO;

namespace Epicod.Cli.Services
{
    /// <summary>
    /// CLI app context.
    /// </summary>
    /// <seealso cref="CliAppContext" />
    public class EpicodCliAppContext : CliAppContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EpicodCliAppContext"/>
        /// class.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <param name="logger">The logger.</param>
        public EpicodCliAppContext(IConfiguration? config, ILogger? logger)
            : base(config, logger)
        {
        }

        /// <summary>
        /// Gets the context service.
        /// </summary>
        /// <param name="dbName">The database name.</param>
        /// <exception cref="ArgumentNullException">dbName</exception>
        public virtual EpicodCliContextService GetContextService(string dbName)
        {
            if (dbName is null) throw new ArgumentNullException(nameof(dbName));

            return new EpicodCliContextService(
                new EpicodCliContextServiceConfig
                {
                    ConnectionString = string.Format(CultureInfo.InvariantCulture,
                    Configuration!.GetConnectionString("Default")!, dbName),
                    LocalDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        "Assets")
                });
        }
    }
}
