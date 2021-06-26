using SqlKata.Compilers;
using SqlKata.Execution;
using System;
using System.Linq;
using System.Collections.Generic;
using Npgsql;

namespace Epicod.Scraper.Sql
{
    /// <summary>
    /// PostgresSql writer for <see cref="TextNode"/>'s.
    /// </summary>
    /// <seealso cref="ITextNodeWriter" />
    public sealed class SqlTextNodeWriter : ITextNodeWriter
    {
        private readonly string _connString;
        private readonly string[] _propCols;
        private QueryFactory _queryFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTextNodeWriter"/>
        /// class.
        /// </summary>
        /// <param name="connString">The connection string.</param>
        /// <exception cref="ArgumentNullException">connString</exception>
        public SqlTextNodeWriter(string connString)
        {
            _connString = connString
                ?? throw new ArgumentNullException(nameof(connString));
            _propCols = new[]
            {
                "nodeid", "name", "value"
            };
        }

        private static string ConstrainLength(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Length > max ? text.Substring(0, max) : text;
        }

        /// <summary>
        /// Writes the specified node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="properties">The optional node properties.</param>
        /// <exception cref="ArgumentNullException">node</exception>
        public void Write(TextNode node, IList<TextNodeProperty> properties = null)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            if (_queryFactory == null)
            {
                _queryFactory = new QueryFactory(
                    new NpgsqlConnection(_connString),
                    new PostgresCompiler());
            }

            _queryFactory.Query("textnode").Insert(new
            {
                id = node.Id,
                parentid = node.ParentId,
                corpus = node.Corpus,
                y = node.Y,
                x = node.X,
                name = ConstrainLength(node.Name, 200),
                uri = ConstrainLength(node.Uri, 300)
            });
            if (properties?.Count > 0)
            {
                object[][] data = (from p in properties
                                   select new object[]
                                   {
                                       p.NodeId, p.Name, p.Value
                                   }).ToArray();
                _queryFactory.Query("textnodeproperty").Insert(_propCols, data);
            }
        }
    }
}
