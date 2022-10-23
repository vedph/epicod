using Epicod.Core;
using Epicod.Scraper.Sql;
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
        private readonly string _connString;

        public PackhumPropInjector(string connString)
        {
            _connString = connString ??
                throw new ArgumentNullException(nameof(connString));
        }

        private static void Clear(QueryFactory queryFactory)
        {
            queryFactory.Query(EpicodSchema.T_PROP)
                .Join(EpicodSchema.T_NODE, $"{EpicodSchema.T_NODE}.id",
                    $"{EpicodSchema.T_PROP}.node_id")
                .Where("corpus", PackhumWebScraper.CORPUS).AsDelete();
        }

        /// <summary>
        /// Injects properties by parsing the node property of each packhum
        /// node.
        /// </summary>
        /// <param name="cancel">The cancellation token.</param>
        /// <param name="progress">The optional progress reporter.</param>
        /// <returns>Count of injected properties.</returns>
        public int Inject(CancellationToken cancel,
            IProgress<ProgressReport>? progress = null)
        {
            QueryFactory qf = new(
                new NpgsqlConnection(_connString),
                new PostgresCompiler());

            // clear
            Clear(qf);

            PackhumNoteParser parser = new();
            //string[] names = new[]
            //{
            //    "region", "location", "type", "layout",
            //    "date-phi", "date-txt", "date-val", "date-nan",
            //    "reference"
            //};
            string[] cols = new[] { "node_id", "name", "value" };

            // get total
            dynamic row = qf.Query(EpicodSchema.T_PROP)
                .Select($"{EpicodSchema.T_NODE}.id as NodeId",
                    $"{EpicodSchema.T_PROP}.Note")
                .Join(EpicodSchema.T_NODE, $"{EpicodSchema.T_NODE}.id",
                    $"{EpicodSchema.T_PROP}.node_id")
                .Where("corpus", PackhumWebScraper.CORPUS).AsCount().First();
            int total = (int)row.count;
            int count = 0, injected = 0;
            ProgressReport? report = progress != null ? new ProgressReport() : null;

            // process each note
            foreach (var item in qf.Query(EpicodSchema.T_PROP)
                .Select($"{EpicodSchema.T_NODE}.id as NodeId",
                    $"{EpicodSchema.T_PROP}.Note")
                .Join(EpicodSchema.T_NODE,
                    $"{EpicodSchema.T_NODE}.id", $"{EpicodSchema.T_PROP}.node_id")
                .Where("corpus", PackhumWebScraper.CORPUS)
                .OrderBy($"{EpicodSchema.T_NODE}.id").Get())
            {
                IList<TextNodeProperty> props = parser.Parse
                    (item.Note, item.NodeId);
                if (props.Count == 0) continue;

                injected += props.Count;
                var data = props.Select(
                    p => new object[] { item.NodeId, p.Name ?? "", p.Value ?? "" })
                    .ToArray();
                qf.Query(EpicodSchema.T_PROP).Insert(cols, data);

                if (progress != null && ++count % 10 == 0)
                {
                    report!.Count = count;
                    report.Percent = count * 100 / total;
                    progress.Report(report);
                }
                if (cancel.IsCancellationRequested) break;
            }

            if (progress != null)
            {
                report!.Percent = 100;
                progress.Report(report);
            }

            return injected;
        }
    }
}
