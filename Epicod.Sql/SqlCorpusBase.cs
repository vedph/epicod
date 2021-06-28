using Epicod.Core;
using Epicod.Scraper.Sql;
using Npgsql;
using SqlKata;
using SqlKata.Compilers;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Epicod.Sql
{
    /// <summary>
    /// Base class for SQL-based corpus handlers.
    /// </summary>
    public abstract class SqlCorpusBase
    {
        private readonly string _connString;
        private bool _disposed;

        /// <summary>
        /// The query factory.
        /// </summary>
        protected QueryFactory _qf;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlCorpusBrowser"/> class.
        /// </summary>
        /// <param name="connString">The connection string.</param>
        /// <exception cref="ArgumentNullException">connString</exception>
        protected SqlCorpusBase(string connString)
        {
            _connString = connString
                ?? throw new ArgumentNullException(nameof(connString));
        }

        /// <summary>
        /// Ensures the query factory is instantiated.
        /// </summary>
        protected void EnsureQueryFactory()
        {
            if (_qf == null)
            {
                _qf = new QueryFactory(
                    new NpgsqlConnection(_connString),
                    new PostgresCompiler());
            }
        }

        /// <summary>
        /// Convert the specified dynamic into a text node.
        /// </summary>
        /// <param name="d">The dynamic or null.</param>
        /// <returns>The text node or null</returns>
        protected static TextNodeResult DynamicToTextNode(dynamic d)
        {
            return d != null ? new TextNodeResult
            {
                Id = d.id,
                ParentId = d.parentid,
                Corpus = d.corpus,
                Y = d.y,
                X = d.x,
                Name = d.name,
                Uri = d.uri,
                IsExpandable = d.expandable ?? false
            } : null;
        }

        /// <summary>
        /// Convert the specified dynamic into a text node property.
        /// </summary>
        /// <param name="d">The dynamic or null.</param>
        /// <returns>The property or null</returns>
        protected static TextNodeResultProperty DynamicToTextNodeProperty(
            dynamic d)
        {
            return d != null ? new TextNodeResultProperty
            {
                Name = d.name,
                Value = d.value
            } : null;
        }

        /// <summary>
        /// Gets the blacks and whites from the specified list of property names,
        /// prefixed by <c>+</c> (or nothing) for whites, and by <c>-</c> for
        /// blacks.
        /// </summary>
        /// <param name="names">The names.</param>
        /// <returns>Tuple with list of black and white names.</returns>
        protected static Tuple<List<string>, List<string>> GetBlackWhites(
            IList<string> names)
        {
            if (names == null)
                return new Tuple<List<string>, List<string>>(null, null);

            List<string> blacks = new List<string>();
            List<string> whites = new List<string>();

            foreach (string name in names.Where(n => n.Length > 0))
            {
                switch (name[0])
                {
                    case '-':
                        // "-" means no property except for whites
                        blacks.Add(name.Substring(1));
                        break;
                    case '+':
                        // "+" means all the properties except for blacks
                        whites.Add(name.Substring(1));
                        break;
                    default:
                        // no prefix means white
                        whites.Add(name);
                        break;
                }
            }
            return Tuple.Create(blacks, whites);
        }

        /// <summary>
        /// Gets the node with the specified ID.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>The node or null</returns>
        protected TextNodeResult GetNode(int id)
        {
            EnsureQueryFactory();

            var query = _qf.Query(EpicodSchema.T_NODE + " AS n")
                   .Select("n.id", "n.parentid", "n.corpus", "n.y", "n.x", 
                        "n.name", "n.uri")
                   .SelectRaw("EXISTS(SELECT(id) " +
                    $"FROM {EpicodSchema.T_NODE} ns " +
                    $"WHERE ns.parentid=n.id) AS expandable")
                   .Where("n.id", id);
            //var sql = _qf.Compiler.Compile(query).RawSql;

            return DynamicToTextNode(query.FirstOrDefault());
        }

        /// <summary>
        /// Loads properties into the specified node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="blacks">The black property names, null to exclude none.
        /// </param>
        /// <param name="whites">The white property names, null to include all.
        /// </param>
        protected void LoadProperties(TextNodeResult node,
            IList<string> blacks, IList<string> whites)
        {
            Query propQuery = _qf.Query(EpicodSchema.T_PROP)
                .Where("nodeid", node.Id).OrderBy("name", "value");

            node.Properties = new List<TextNodeResultProperty>();
            if (blacks != null || whites != null)
            {
                if (whites != null)
                {
                    // empty whites = no properties
                    if (whites.Count == 0) return;

                    // "+" = any properties (except blacks), so filter names
                    // only when no white is equal to ""
                    if (!whites.Contains("")) propQuery.WhereIn("name", whites);
                }

                // "-" = no properties (except whites), so filter names
                // only when no black is equal to ""
                if (blacks?.Count > 0 && !blacks.Contains(""))
                    propQuery.WhereNotIn("name", blacks);
            }

            foreach (var d in propQuery.Get())
                node.Properties.Add(DynamicToTextNodeProperty(d));
        }

        /// <summary>
        /// Loads properties into the specified node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="propFilters">The optional property filters.</param>
        protected void LoadProperties(TextNodeResult node,
            IList<string> propFilters = null)
        {
            if (propFilters == null)
            {
                // all properties
                Query propQuery = _qf.Query(EpicodSchema.T_PROP)
                    .Where("nodeid", node.Id).OrderBy("name", "value");
                foreach (var d in propQuery.Get())
                    node.Properties.Add(DynamicToTextNodeProperty(d));
            }
            else
            {
                var bw = GetBlackWhites(propFilters);
                LoadProperties(node, bw.Item1, bw.Item2);
            }
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _qf?.Dispose();
                    _qf = null;
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing,
        /// releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // do not change this code. Put cleanup code in 'Dispose(disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
