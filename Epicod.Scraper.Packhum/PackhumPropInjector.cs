using Epicod.Core;
using Epicod.Scraper.Sql;
using Fusi.Tools;
using Microsoft.Extensions.Logging;
using Npgsql;
using SqlKata;
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
    public sealed class PackhumPropInjector : IPropInjector
    {
        private readonly string _connString;

        public bool IsDry { get; set; }
        public ILogger? Logger { get; set; }

        public PackhumPropInjector(string connString)
        {
            _connString = connString ??
                throw new ArgumentNullException(nameof(connString));
        }

        private static void Clear(QueryFactory qf)
        {
            Query query = qf.Query(EpicodSchema.T_PROP + " AS tp")
              .Join(EpicodSchema.T_NODE + " AS tn", "tn.id", "tp.node_id")
              .Where("tn.corpus", PackhumWebScraper.CORPUS)
              .WhereLike("tp.name", $"{TextNodeProps.DATE_TXT}%")
              .OrWhereLike("tp.name", $"{TextNodeProps.DATE_VAL}%")
              .OrWhereIn("tp.name", new[]
                {
                  TextNodeProps.DATE_PHI,
                  TextNodeProps.REGION,
                  TextNodeProps.SITE,
                  TextNodeProps.LOCATION,
                  TextNodeProps.REFERENCE,
                  TextNodeProps.FORGERY,
                  TextNodeProps.STOICH_MIN,
                  TextNodeProps.STOICH_MAX,
                  TextNodeProps.NON_STOICH_MIN,
                  TextNodeProps.NON_STOICH_MAX,
              })
              .Select("tp.id");
            qf.Query(EpicodSchema.T_PROP).WhereIn("id", query).Delete();
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
            if (!IsDry) Clear(qf);

            PackhumParser parser = new()
            {
                Logger = Logger
            };
            string[] cols = new[] { "node_id", "name", "value", "type" };

            // get total
            dynamic row = qf.Query(EpicodSchema.T_PROP)
                .Select($"{EpicodSchema.T_NODE}.id")
                .Join(EpicodSchema.T_NODE, $"{EpicodSchema.T_NODE}.id",
                    $"{EpicodSchema.T_PROP}.node_id")
                .Where("corpus", PackhumWebScraper.CORPUS)
                .Where($"{EpicodSchema.T_PROP}.name", "note")
                .AsCount().First();
            int total = (int)row.count;
            int count = 0, injected = 0;
            ProgressReport? report = progress != null ? new ProgressReport() : null;

            // process each note
            foreach (var item in qf.Query(EpicodSchema.T_PROP)
                .Select($"{EpicodSchema.T_NODE}.id as NodeId",
                    $"{EpicodSchema.T_PROP}.value as Note")
                .Join(EpicodSchema.T_NODE,
                    $"{EpicodSchema.T_NODE}.id", $"{EpicodSchema.T_PROP}.node_id")
                .Where("corpus", PackhumWebScraper.CORPUS)
                .Where($"{EpicodSchema.T_PROP}.name", "note")
                .OrderBy($"{EpicodSchema.T_NODE}.id").Get())
            {
                try
                {
                    IList<TextNodeProperty> props = parser.ParseNote(
                        item.Note, item.NodeId);
                    if (props.Count == 0) continue;

                    injected += props.Count;
                    var data = props.Select(p => new object?[]
                        {
                            item.NodeId,
                            p.Name!,
                            p.Value!,
                            p.Name == TextNodeProps.DATE_VAL
                                ? TextNodeProps.TYPE_INT : null
                        })
                        .ToArray();
                    if (!IsDry) qf.Query(EpicodSchema.T_PROP).Insert(cols, data);
                }
                catch (Exception ex)
                {
                    Logger?.LogCritical(ex,
                        "Error parsing PHI note \"{Note}\"",
                        (object)item.Note);
                    throw;
                }

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
