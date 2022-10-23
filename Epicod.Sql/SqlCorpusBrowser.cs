using Epicod.Core;
using Epicod.Scraper.Sql;
using Fusi.Tools.Data;
using SqlKata;
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
    public sealed class SqlCorpusBrowser : SqlCorpusBase, ICorpusBrowser
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlCorpusBrowser"/> class.
        /// </summary>
        /// <param name="connString">The connection string.</param>
        public SqlCorpusBrowser(string connString) : base(connString)
        {
        }

        /// <summary>
        /// Gets the full list of corpora IDs.
        /// </summary>
        /// <returns>Corpora IDs, alphabetically sorted.</returns>
        public IList<string> GetCorpora()
        {
            EnsureQueryFactory();
            return _qf!.Query(EpicodSchema.T_NODE)
                    .Select("corpus")
                    .OrderBy("corpus")
                    .Distinct()
                    .Get<string>().ToList();
        }

        private static void ApplyFilter(TextNodeFilter filter, Query query)
        {
            query.Where("parent_id", filter.ParentId);
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
            Query totQuery = _qf!.Query(EpicodSchema.T_NODE);
            ApplyFilter(filter, totQuery);
            int total = (int)totQuery.AsCount().First().count;

            // get page
            Query query = _qf.Query(EpicodSchema.T_NODE)
                .Select("id", "parent_id", "corpus", "y", "x", "name", "uri")
                .SelectRaw("EXISTS(SELECT(id) " +
                    $"FROM {EpicodSchema.T_NODE} n " +
                    $"WHERE n.parent_id={EpicodSchema.T_NODE}.id) AS expandable");
            ApplyFilter(filter, query);
            query.OrderBy("y", "parent_id", "x");
            query.Skip(filter.GetSkipCount()).Limit(filter.PageSize);

            List<TextNodeResult> nodes = new();
            foreach (dynamic d in query.Get())
                nodes.Add(DynamicToTextNode(d));

            return new DataPage<TextNodeResult>(
                filter.PageNumber, filter.PageSize,
                total, nodes);
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
        public TextNodeResult? GetNode(int id, IList<string>? propFilters = null)
        {
            EnsureQueryFactory();

            var query = _qf!.Query(EpicodSchema.T_NODE + " AS n")
                   .Select("n.id", "n.parent_id", "n.corpus", "n.y", "n.x", "n.name", "n.uri")
                   .SelectRaw("EXISTS(SELECT(id) " +
                    $"FROM {EpicodSchema.T_NODE} ns " +
                    "WHERE ns.parent_id=n.id) AS expandable")
                   .Where("n.id", id);
            //var sql = _qf.Compiler.Compile(query).RawSql

            TextNodeResult node = DynamicToTextNode(query.FirstOrDefault());
            if (node == null) return null;

            // properties
            LoadProperties(node, propFilters);
            return node;
        }
    }
}
