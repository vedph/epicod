namespace Epicod.Cli.Services
{
    /// <summary>
    /// CLI context service.
    /// </summary>
    public class EpicodCliContextService
    {
        private readonly EpicodCliContextServiceConfig _config;

        public string? ConnectionString => _config.ConnectionString;

        /// <summary>
        /// Initializes a new instance of the <see cref="EpicodCliContextService"/>
        /// class.
        /// </summary>
        /// <param name="config">The configuration.</param>
        public EpicodCliContextService(EpicodCliContextServiceConfig config)
        {
            _config = config;
        }
    }

    /// <summary>
    /// Configuration for <see cref="EpicodCliContextService"/>.
    /// </summary>
    public class EpicodCliContextServiceConfig
    {
        /// <summary>
        /// Gets or sets the connection string to the database.
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the local directory to use when loading resources
        /// from the local file system.
        /// </summary>
        public string? LocalDirectory { get; set; }
    }
}
