using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Epicod.Scraper.Sql
{
    public static class ScraperDbSchema
    {
        /// <summary>
        /// Gets the DDL SQL representing the schema.
        /// </summary>
        /// <returns>SQL script for creating tables in database.</returns>
        /// <exception cref="ArgumentNullException">dbType</exception>
        /// <exception cref="ArgumentException">Invalid database type</exception>
        public static string Get()
        {
            using (StreamReader reader = new StreamReader(
                Assembly.GetExecutingAssembly().GetManifestResourceStream(
                    "Epicod.Scraper.Sql.Assets.Schema.pgsql"), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
