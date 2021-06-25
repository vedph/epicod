using Fusi.Tools;
using Npgsql;
using SqlKata.Compilers;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Epicod.Scraper.Packhum
{
    /// <summary>
    /// Packhum properties injector.
    /// </summary>
    public sealed class PackhumPropInjector
    {
        private const string NODE_TABLE = "textnode";
        private const string PROP_TABLE = "textnodeproperty";

        private readonly string _connString;

        public PackhumPropInjector(string connString)
        {
            _connString = connString ??
                throw new ArgumentNullException(nameof(connString));
        }

        private static void Clear(QueryFactory queryFactory)
        {
            queryFactory.Query(PROP_TABLE)
                .Join("textnode", $"{NODE_TABLE}.id", $"{PROP_TABLE}.nodeid")
                .Where("corpus", PackhumScraper.CORPUS).AsDelete();
        }

        /// <summary>
        /// Injects properties by parsing the node property of each packhum
        /// node.
        /// </summary>
        /// <param name="cancel">The cancellation token.</param>
        /// <param name="progress">The optional progress reporter.</param>
        /// <returns>Count of injected properties.</returns>
        public int Inject(CancellationToken cancel,
            IProgress<ProgressReport> progress = null)
        {
            QueryFactory qf = new QueryFactory(
                new NpgsqlConnection(_connString),
                new PostgresCompiler());

            // clear
            Clear(qf);

            PackhumNoteParser parser = new PackhumNoteParser();
            string[] names = new[]
            {
                "region", "location", "type", "layout",
                "date-phi", "date-txt", "date-val", "date-nan",
                "reference"
            };
            string[] cols = new[] { "nodeid", "name", "value" };

            // get total
            dynamic row = qf.Query(PROP_TABLE)
                .Select($"{NODE_TABLE}.id as NodeId", $"{PROP_TABLE}.Note")
                .Join(NODE_TABLE, $"{NODE_TABLE}.id", $"{PROP_TABLE}.nodeid")
                .Where("corpus", PackhumScraper.CORPUS).AsCount().First();
            int total = (int)row.count;
            int count = 0, injected = 0;
            ProgressReport report = progress != null ? new ProgressReport() : null;

            // process each note
            foreach (var item in qf.Query(PROP_TABLE)
                .Select($"{NODE_TABLE}.id as NodeId", $"{PROP_TABLE}.Note")
                .Join(NODE_TABLE, $"{NODE_TABLE}.id", $"{PROP_TABLE}.nodeid")
                .Where("corpus", PackhumScraper.CORPUS)
                .OrderBy($"{NODE_TABLE}.id").Get())
            {
                IList<TextNodeProperty> props = parser.Parse
                    (item.Note, item.NodeId);
                if (props.Count == 0) continue;

                injected += props.Count;
                var data = props.Select(
                    p => new object[] { item.NodeId, p.Name, p.Value })
                    .ToArray();
                qf.Query(PROP_TABLE).Insert(cols, data);

                if (progress != null && ++count % 10 == 0)
                {
                    report.Count = count;
                    report.Percent = count * 100 / total;
                    progress.Report(report);
                }
                if (cancel.IsCancellationRequested) break;
            }

            if (progress != null)
            {
                report.Percent = 100;
                progress.Report(report);
            }

            return injected;
        }
    }
}
