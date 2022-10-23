using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Epicod.Scraper.Sql
{
    /// <summary>
    /// Epicod SQL schema.
    /// </summary>
    public static class EpicodSchema
    {
        /// <summary>
        /// The name of the text nodes table.
        /// </summary>
        public const string T_NODE = "text_node";

        /// <summary>
        /// The name of the text nodes properties table.
        /// </summary>
        public const string T_PROP = "text_node_property";

        /// <summary>
        /// Gets the DDL SQL representing the schema.
        /// </summary>
        /// <returns>SQL script for creating tables in database.</returns>
        /// <exception cref="ArgumentNullException">dbType</exception>
        /// <exception cref="ArgumentException">Invalid database type</exception>
        public static string Get()
        {
            using StreamReader reader = new(
                Assembly.GetExecutingAssembly().GetManifestResourceStream(
                    "Epicod.Sql.Assets.Schema.pgsql")!, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
