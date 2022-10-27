using Epicod.Core;
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
using System.Security.Cryptography;
using System.Text;
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
            Query query = qf.Query(EpicodSchema.T_PROP + " AS tp")
              .Join(EpicodSchema.T_NODE + " AS tn", "tn.id", "tp.node_id")
              .Where("tn.corpus", ClaussWebScraper.CORPUS)
              .Where(q => q.WhereLike("tp.name", $"{TextNodeProps.DATE_TXT}%")
                .OrWhereLike("tp.name", $"{TextNodeProps.DATE_VAL}%")
                .OrWhere("tp.name", TextNodeProps.LANGUAGES))
              .Select("tp.id");
            qf.Query(EpicodSchema.T_PROP).WhereIn("id", query).Delete();
        }

        private static bool IsInGreekRange(char c)
        {
            return (c > 0x36F && c < 0x400) || (c > 0x1EFF && c < 0x2000);
        }

        private IList<int> ParseYears(string? text)
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
                        Logger?.LogError("Invalid date value: {Value}", year);
                    }
                    else years.Add(n);
                }
                return years;
            }
            else
            {
                if (!int.TryParse(text, out int n))
                {
                    Logger?.LogError("Invalid date value: {Value}", text);
                    return Array.Empty<int>();
                }
                else return new[] { n };
            }
        }

        private IList<HistoricalDate> BuildDates(string? dating, string? to)
        {
            IList<int> aYears = ParseYears(dating);
            IList<int> bYears = ParseYears(to);

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
                    dates.Add(HistoricalDate.Parse($"{a} --")!);
            }

            // both a and b
            foreach (int a in aYears)
            {
                foreach (int b in bYears)
                {
                    dates.Add(HistoricalDate.Parse(a != b
                        ? $"{a} -- {b}"
                        : $"{a}")!);
                }
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

        private void CollectDateProps(int nodeId, string a, string b,
            IList<object[]> props)
        {
            int n = 0;
            foreach (var date in BuildDates(a, b))
            {
                n++;
                props.Add(new object[]
                {
                    nodeId,
                    TextNodeProps.DATE_VAL + (n > 1? $"#{n}" : ""),
                    date.GetSortValue()
                });
                props.Add(new object[]
                {
                    nodeId,
                    TextNodeProps.DATE_TXT + (n > 1? $"#{n}" : ""),
                    date.ToString()
                });
            }
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
            StringBuilder a = new(), b = new();

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
                        CollectDateProps(oldId, a.ToString(), b.ToString(), props);
                        injected += props.Count;
                        if (!IsDry && props.Count > 0)
                            qf.Query(EpicodSchema.T_PROP).Insert(cols, props);
                    }
                    props.Clear();
                    a.Clear();
                    b.Clear();
                    oldId = id;
                }

                switch (row.name)
                {
                    // dating and to are joined into date-val
                    case "dating":
                        if (a.Length > 0) a.Append(';');
                        a.Append(row.value);
                        break;
                    case "to":
                        if (b.Length > 0) b.Append(';');
                        b.Append(row.value);
                        break;
                    // languages provided by scanning text
                    case "text":
                        string langs = GetLanguages(row.value);
                        props.Add(
                            new object[] { id, TextNodeProps.LANGUAGES, langs });
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
                CollectDateProps(oldId, a.ToString(), b.ToString(), props);
                injected += props.Count;
                if (!IsDry && props.Count > 0)
                    qf.Query(EpicodSchema.T_PROP).Insert(cols, props);
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
