using Epicod.Scraper.Sql;
using Fusi.Antiquity.Chronology;
using Fusi.Tools;
using Npgsql;
using SqlKata;
using SqlKata.Compilers;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Epicod.Scraper.Clauss
{
    public class ClaussPropInjector : IPropInjector
    {
        private readonly string _connString;
        private readonly Regex _grRegex;

        public ClaussPropInjector(string connString)
        {
            _connString = connString ??
                throw new ArgumentNullException(nameof(connString));
            _grRegex = new Regex(@"\bGR\b", RegexOptions.Compiled);
        }

        private static void Clear(QueryFactory qf)
        {
            qf.Query(EpicodSchema.T_PROP + " AS tp")
              .Join(EpicodSchema.T_NODE + " AS tn", "tn.id", "tp.node_id")
              .Where("tn.corpus", ClaussWebScraper.CORPUS)
              .WhereIn("tp.name", new[] {"date-val", "languages"})
              .AsDelete();
        }

        private static bool IsInGreekRange(char c)
        {
            return (c > 0x36F && c < 0x400) || (c > 0x1EFF && c < 0x2000);
        }

        private HistoricalDate? BuildDate(string? dating, string? to)
        {
            int a = string.IsNullOrEmpty(dating) ?
                0 : int.Parse(dating, CultureInfo.InvariantCulture);
            int b = string.IsNullOrEmpty(to) ?
                0 : int.Parse(to, CultureInfo.InvariantCulture);

            if (a == 0 && b == 0) return null;
            if (a == 0) return HistoricalDate.Parse($"-- {b}");
            if (b == 0) return HistoricalDate.Parse($"{a} --");

            return HistoricalDate.Parse(a == b
                ? $"{a}"
                : $"{a} -- {b}");
        }

        private string GetLanguages(string text)
        {
            text = text.Replace("vacat", "");
            text = _grRegex.Replace(text, "");

            bool lat = text.Any(
                c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));
            bool grc = text.Any(IsInGreekRange);

            if (!lat && !grc) return "";
            if (lat && grc) return "grc lat";
            return lat ? "lat" : "grc";
        }

        public int Inject(CancellationToken cancel,
            IProgress<ProgressReport>? progress = null)
        {
            QueryFactory qf = new(
                new NpgsqlConnection(_connString),
                new PostgresCompiler());

            Clear(qf);
            string[] cols = new[] { "node_id", "name", "value" };

            int count = 0, injected = 0;
            ProgressReport? report = progress != null
                ? new ProgressReport() : null;

            // all the Clauss nodes with text/dating/to
            Query query = qf.Query(EpicodSchema.T_NODE + " AS tn")
              .Join(EpicodSchema.T_PROP + " AS tp", "tn.id", "tp.node_id")
              .Where("tn.corpus", ClaussWebScraper.CORPUS)
              .WhereIn("tp.name", new[] { "text", "dating", "to" });

            // get the total count
            Query totQuery = query.Clone().AsCount();
            var rowTot = totQuery.First();
            int total = (int)rowTot.count;

            query = query.Select("tn.id", "tp.name", "tp.value")
                         .OrderBy("tn.id");

            List<object[]> props = new();
            string? a = null, b = null;

            // for each node
            foreach (var row in query.Get())
            {
                int id = row[0];
                switch (row[1] as string)
                {
                    // dating and to are joined into date-val
                    case "dating":
                        a = row[2];
                        break;
                    case "to":
                        b = row[2];
                        break;
                    // languages provided by scanning text
                    case "text":
                        string langs = GetLanguages(row[2]);
                        props.Add(new object[] { id, "languages", langs });
                        break;
                }

                HistoricalDate? date = BuildDate(a, b);
                if (date is not null)
                {
                    props.Add(new object[] { id, "date-val", date.GetSortValue() });
                    props.Add(new object[] { id, "date-txt", date.ToString() });
                }
                qf.Query(EpicodSchema.T_PROP).Insert(cols, props);
                injected += props.Count;

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
