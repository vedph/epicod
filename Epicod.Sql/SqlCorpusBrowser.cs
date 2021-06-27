using Epicod.Core;
using Epicod.Scraper.Sql;
using Fusi.Tools.Data;
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
    /// SQL-based corpus browser.
    /// </summary>
    /// <seealso cref="ICorpusBrowser" />
    public sealed class SqlCorpusBrowser : ICorpusBrowser
    {
        private readonly string _connString;
        private QueryFactory _qf;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlCorpusBrowser"/> class.
        /// </summary>
        /// <param name="connString">The connection string.</param>
        /// <exception cref="ArgumentNullException">connString</exception>
        public SqlCorpusBrowser(string connString)
        {
            _connString = connString
                ?? throw new ArgumentNullException(nameof(connString));
        }

        private void EnsureQueryFactory()
        {
            if (_qf== null)
            {
                _qf = new QueryFactory(
                    new NpgsqlConnection(_connString),
                    new PostgresCompiler());
            }
        }

        private static TextNodeResult DynamicToTextNode(dynamic d)
        {
            return d != null? new TextNodeResult
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

        private static TextNodeResultProperty DynamicToTextNodeProperty(dynamic d)
        {
            return d != null ? new TextNodeResultProperty
            {
                Name = d.name,
                Value = d.value
            } : null;
        }

        /// <summary>
        /// Gets the full list of corpora IDs.
        /// </summary>
        /// <returns>Corpora IDs, alphabetically sorted.</returns>
        public IList<string> GetCorpora()
        {
            EnsureQueryFactory();
            return _qf.Query(EpicodSchema.T_NODE)
                    .Select("corpus")
                    .OrderBy("corpus")
                    .Distinct()
                    .Get<string>().ToList();
        }

        private static void ApplyFilter(TextNodeFilter filter, Query query)
        {
            query.Where("parentid", filter.ParentId);
            if (!string.IsNullOrEmpty(filter.CorpusId))
                query.Where("corpus", filter.CorpusId);
        }

        /// <summary>
        /// Gets the specified page of nodes.
        /// </summary>
        /// <param name="filter">The filter.</param>
        /// <returns>
        /// The resulting page.
        /// </returns>
        /// <exception cref="ArgumentNullException">filter</exception>
        public DataPage<TextNodeResult> GetNodes(TextNodeFilter filter)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            EnsureQueryFactory();

            // get total
            Query totQuery = _qf.Query(EpicodSchema.T_NODE);
            ApplyFilter(filter, totQuery);
            int total = (int)totQuery.AsCount().First().count;

            // get page
            Query query = _qf.Query(EpicodSchema.T_NODE)
                .Select("id", "parentid", "corpus", "y", "x", "name", "uri")
                .SelectRaw("EXISTS(SELECT(id) " +
                    $"FROM {EpicodSchema.T_NODE} n " +
                    $"WHERE n.parentid={EpicodSchema.T_NODE}.id) AS expandable");
            ApplyFilter(filter, query);
            query.OrderBy("x");
            query.Skip(filter.GetSkipCount()).Limit(filter.PageSize);

            List<TextNodeResult> nodes = new List<TextNodeResult>();
            foreach (dynamic d in query.Get())
                nodes.Add(DynamicToTextNode(d));

            return new DataPage<TextNodeResult>(
                filter.PageNumber, filter.PageSize,
                total, nodes);
        }

        private static Tuple<List<string>,List<string>> GetBlackWhites(
            IList<string> names)
        {
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
        /// Gets the node with all or a part of its properties.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="propFilters">The property filters; this is null when
        /// all the properties must be retrieved; otherwise, it is a list of
        /// property names, each prefixed with <c>+</c> to include it, or by
        /// <c>-</c> to exclude it. Neither of these prefixes just means to
        /// include the named property. You can use <c>+</c> alone to include
        /// any properties, except the excluded ones if any; or <c>-</c> alone
        /// to exclude any properties, except the included ones if any.</param>
        /// <returns>
        /// The result or null if not found.
        /// </returns>
        public TextNodeResult GetNode(int id, IList<string> propFilters = null)
        {
            EnsureQueryFactory();
            TextNodeResult node = DynamicToTextNode(
                _qf.Query(EpicodSchema.T_NODE)
                   .Select("id", "parentid", "corpus", "y", "x", "name", "uri")
                   .SelectRaw("EXISTS(SELECT(id) " +
                    $"FROM {EpicodSchema.T_NODE} n " +
                    $"WHERE n.parentid={EpicodSchema.T_NODE}.id) AS expandable")
                   .Where($"{EpicodSchema.T_NODE}.id", id));
            if (node == null) return null;

            // properties
            Query query = _qf.Query(EpicodSchema.T_PROP)
                .Where("nodeid", id).OrderBy("name", "value");

            node.Properties = new List<TextNodeResultProperty>();
            if (propFilters != null)
            {
                var bw = GetBlackWhites(propFilters);

                // no whites = no properties
                if (bw.Item2.Count == 0) return node;

                // "+" = any properties (except blacks), so filter names
                // only when no white is equal to ""
                if (!bw.Item2.Contains("")) query.WhereIn("name", bw.Item2);

                // "-" = no properties (except whites), so filter names
                // only when no black is equal to ""
                if (bw.Item1.Count > 0 && !bw.Item1.Contains(""))
                    query.WhereNotIn("name", bw.Item1);
            }

            foreach (var d in query.Get())
                node.Properties.Add(DynamicToTextNodeProperty(d));

            return node;
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
