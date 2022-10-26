using Epicod.Scraper.Sql;
using Fusi.Antiquity.Chronology;
using Fusi.Tools;
using Microsoft.Extensions.Logging;
using Npgsql;
using SqlKata;
using SqlKata.Compilers;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Epicod.Scraper.Clauss
{
    public class ClaussPropInjector : IPropInjector
    {
        private readonly string _connString;
        private readonly Regex _grRegex;

        public bool IsDry { get; set; }
        public ILogger? Logger { get; set; }

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

        private IList<int> ParseYear(string? text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<int>();

            if (text.Contains(';'))
            {
                List<int> years = new();
                foreach (string year in text.Split(';',
                    StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!int.TryParse(year, out int n))
                    {
                        Logger?.LogError("Invalid dating value: {Value}", year);
                    }
                    else years.Add(n);
                }
                return years;
            }
            else
            {
                if (!int.TryParse(text, out int n))
                {
                    Logger?.LogError("Invalid dating value: {Value}", text);
                    return Array.Empty<int>();
                }
                else return new[] { n };
            }
        }

        private IList<HistoricalDate> BuildDates(string? dating, string? to)
        {
            IList<int> aYears = ParseYear(dating);
            IList<int> bYears = ParseYear(to);

            // not a nor b
            if (aYears.Count == 0 && bYears.Count == 0)
                return Array.Empty<HistoricalDate>();

            List<HistoricalDate> dates = new();

            // not a but b
            if (aYears.Count == 0)
            {
                foreach (int b in bYears)
                    dates.Add(HistoricalDate.Parse($"-- {b}")!);
            }

            // not b but a
            if (bYears.Count == 0)
            {
                foreach (int a in aYears)
                    dates.Add(HistoricalDate.Parse($"-- {a}")!);
            }

            // both a and b
            foreach (int a in aYears)
            {
                foreach (int b in bYears)
                    dates.Add(HistoricalDate.Parse($"{a} -- {b}")!);
            }

            return dates;
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

            if (!IsDry) Clear(qf);
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
            int oldId = 0;
            foreach (var row in query.Get())
            {
                int id = row.id;

                // whenever the node changes, save its properties if any
                if (oldId != id)
                {
                    if (oldId > 0)
                    {
                        foreach (var date in BuildDates(a, b))
                        {
                            props.Add(new object[]
                            {
                                oldId, "date-val", date.GetSortValue()
                            });
                            props.Add(new object[]
                            {
                                oldId, "date-txt", date.ToString()
                            });
                        }
                        if (!IsDry && props.Count > 0)
                        {
                            qf.Query(EpicodSchema.T_PROP).Insert(cols, props);
                            injected += props.Count;
                        }
                    }
                    props.Clear();
                    oldId = id;
                }

                switch (row.name)
                {
                    // dating and to are joined into date-val
                    case "dating":
                        a = row.value;
                        break;
                    case "to":
                        b = row.value;
                        break;
                    // languages provided by scanning text
                    case "text":
                        string langs = GetLanguages(row.value);
                        props.Add(new object[] { id, "languages", langs });
                        break;
                }

                if (progress != null && ++count % 10 == 0)
                {
                    report!.Count = count;
                    report.Percent = count * 100 / total;
                    progress.Report(report);
                }
                if (cancel.IsCancellationRequested) break;
            }

            // save last pending data if any
            if (oldId > 0)
            {
                foreach (var date in BuildDates(a, b))
                {
                    props.Add(new object[]
                    {
                                oldId, "date-val", date.GetSortValue()
                    });
                    props.Add(new object[]
                    {
                                oldId, "date-txt", date.ToString()
                    });
                }
                if (!IsDry && props.Count > 0)
                {
                    qf.Query(EpicodSchema.T_PROP).Insert(cols, props);
                    injected += props.Count;
                }
            }

            // completed
            if (progress != null)
            {
                report!.Percent = 100;
                progress.Report(report);
            }

            return injected;
        }
    }
}
