using Fusi.Antiquity.Chronology;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Epicod.Scraper.Packhum
{
    /// <summary>
    /// Packard Humanities date parser.
    /// </summary>
    public sealed class PackhumDateParser
    {
        #region Constants
        // preprocessing for split
        private static readonly Regex _wsRegex =
            new(@"\s+", RegexOptions.Compiled);

        private static readonly Regex _orModifierRegex = new(
            @"(?:or |od\.|oder )" +
            @"(?<l>shortly |slightly |sh\.)? ?" +
            @"(?<m>later|lat\.|after|aft.|earlier|früher|später)\??",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _earlyModifierRegex = new(
            ", early$", RegexOptions.Compiled);

        private static readonly Regex _dateSuffixRegex = new
            (@",\s*(?<d>[0-3]?[0-9])?\s*" +
             @"(?<m>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[^\s]*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _wRegex = new
            (@"\bw//?", RegexOptions.Compiled);

        private static readonly string[] _dateSeps = new[]
        {
            " and ", " or ", " od.", " oder ", " & ", ","
        };

        // date preprocessing
        private static readonly Regex _apSuffixRegex = new(@"([0-9])(a\.|p\.)",
            RegexOptions.Compiled);

        private static readonly Regex _caInitialRegex = new(
            @"^ca?\.", RegexOptions.Compiled);

        private static readonly Regex _splitQmkRegex = new(
            @"([0-9]\?)\s+([0-9])", RegexOptions.Compiled);

        private static readonly Regex _splitSlashRegex = new(
            "([^0-9IVX])/([^0-9IVX])", RegexOptions.Compiled);

        // datation
        private static readonly Regex _splitPtRegex = new(
            "([0-9IVXtdh?])-([0-9IVX])", RegexOptions.Compiled);

        private static readonly Regex _qmkPrefixRegex = new(
            "(init.|beg.|Anf.|med.|mid|middle|" +
            "fin.|end|Ende|Wende|" +
            @"early|eher|early\s*/\s*mid|late|" +
            @"1st half|2nd half|1.\s*Halfte|2.\s*Halfte|" +
            @"mid\s*/\s*2nd half|middle\s*/\s*2nd half)\?",
            RegexOptions.Compiled);

        private static readonly Regex _midDashRegex = new(@"\bmid-([0-9])",
            RegexOptions.Compiled);

        private static readonly Dictionary<string, HistoricalDate>
            _periods = new();
        #endregion

        // state
        private readonly List<string> _hints;

        public PackhumDateParser()
        {
            _hints = new List<string>();
        }

        private static int GetMonthNumber(string month)
        {
            return month[..3].ToLowerInvariant() switch
            {
                "jan" => 1,
                "feb" => 2,
                "mar" => 3,
                "apr" => 4,
                "may" => 5,
                "jun" => 6,
                "jul" => 7,
                "aug" => 8,
                "sep" => 9,
                "oct" => 10,
                "nov" => 11,
                "dec" => 12,
                _ => 0,
            };
        }

        private static string NormalizeWS(string s)
            => _wsRegex.Replace(s, " ").Trim();

        private string ExtractHints(string text)
        {
            char[] clChars = new[] { ']', ')' };

            // find 1st ] or )
            int i = text.LastIndexOfAny(clChars);
            if (i == -1) return text;

            StringBuilder sb = new(text);
            while (i > -1)
            {
                // find next [ or (
                int end = i;
                char c = text[i] == ']' ? '[' : '(';
                i = text.LastIndexOf(c, i - 1);
                if (i == -1) i = 0;

                // extract and remove hint
                _hints.Add(text[i..(end + 1)]);
                sb.Remove(i, end + 1 - i);

                // find next ] or )
                i = text.LastIndexOfAny(clChars, i - 1);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Preprocesses date text for split (see A1 in docs).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <exception cref="ArgumentNullException">text</exception>
        public string PreprocessForSplit(string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));

            // normalize WS
            string s = NormalizeWS(text);

            // replace (?) with ?
            s = s.Replace("(?)", "?");

            // extract [...] and (...)
            s = ExtractHints(s);

            // corner cases
            s = _wRegex.Replace(s, "w");
            s = s.Replace("July/August", "July");

            // or...earlier/later: wrap in () normalizing expression
            s = _orModifierRegex.Replace(s, (Match m) =>
            {
                return "(or " +
                       (m.Groups["l"].Length > 0 ? "shortly " : "") +
                       (m.Groups["m"].Value[0] == 'f' ||
                        m.Groups["m"].Value[0] == 'e' ? "earlier" : "later")
                        + ")";
            });

            // at the earliest => wrap in ()
            s = s.Replace("at the earliest", "(at the earliest)");

            // , early... : wrap in ()
            s = _earlyModifierRegex.Replace(s, " (early)");

            // date suffix: wrap in {d=N,m=N} ({} are never used in PHI dates)
            s = _dateSuffixRegex.Replace(s, (Match m) =>
            {
                int day = m.Groups["d"].Length > 0
                    ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)
                    : 0;
                int month = GetMonthNumber(m.Groups[2].Value);
                return "{" + (day > 0 ? $"d={day},m={month}" : $"m={month}") + "}";
            });

            // re-extract hints eventually injected by preprocessing,
            // and re-normalize whitespace
            return NormalizeWS(ExtractHints(s));
        }

        private static IList<string> SplitAtRegexWithSep(string text, Regex r)
        {
            if (!r.IsMatch(text)) return Array.Empty<string>();

            string lines = r.Replace(text, (Match m) =>
                $"{m.Groups[1].Value}\n{m.Groups[2].Value}");
            return lines.Split('\n');
        }

        /// <summary>
        /// Splits the dates in the specified date(s) text (see A2 in docs).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>Date(s) text(s).</returns>
        /// <exception cref="ArgumentNullException">text</exception>
        public static IList<string> SplitDates(string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));

            // corner case: split at question mark
            if (_splitQmkRegex.IsMatch(text))
                return SplitAtRegexWithSep(text, _splitQmkRegex);

            // corner case: split at slash
            if (_splitSlashRegex.IsMatch(text))
                return SplitAtRegexWithSep(text, _splitSlashRegex);

            // normal split at conjunctions or comma
            return (from split in
                text.Split(_dateSeps, StringSplitOptions.RemoveEmptyEntries)
                select NormalizeWS(split))
                .ToList();
        }

        /// <summary>
        /// Splits the datations in the specified single date text (see B in docs).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>Datation(s) text(s).</returns>
        /// <exception cref="ArgumentNullException">text</exception>
        public static IList<string> SplitDatations(string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));

            return SplitAtRegexWithSep(text, _splitPtRegex);
        }

        private static void EnsurePeriodsLoaded()
        {
            if (_periods.Count != 0) return;

            using StreamReader reader = new(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(
                "Epicod.Scraper.Packhum.Assets.Periods.csv")!,
                Encoding.UTF8);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] cols = line.Split(',');
                _periods[cols[0].ToLowerInvariant()] =
                    HistoricalDate.Parse(cols[1])!;
            }
        }

        private static HistoricalDate? MatchPeriod(string text)
        {
            EnsurePeriodsLoaded();
            string s = NormalizeWS(text).ToLowerInvariant();
            return _periods!.ContainsKey(s) ? _periods[s] : null;
        }

        /// <summary>
        /// Preprocesses the specified datations.
        /// </summary>
        /// <param name="texts">The datation texts.</param>
        /// <returns>One tuple for each input text, with 1=preprocessed text,
        /// 2=about, 3=dubious.</returns>
        /// <exception cref="ArgumentNullException">text</exception>
        public static IList<Tuple<string, bool, bool>> PreprocessDatations(
            IList<string> texts)
        {
            if (texts is null) throw new ArgumentNullException(nameof(texts));

            int n = 0;
            bool globalCa = false,
                 globalDub = texts[^1].EndsWith("?", StringComparison.Ordinal);
            List<Tuple<string, bool, bool>> results = new();

            foreach (string text in texts)
            {
                n++;

                // detach a./p. when attached to N
                string s = _apSuffixRegex.Replace(text, "$1 $2");

                // remove initial ca. (this must be applied to any datations)
                bool ca = globalCa;
                Match m = _caInitialRegex.Match(s);
                if (m.Success)
                {
                    ca = true;
                    if (n == 1) globalCa = true;
                    s = s.Remove(0, m.Length);
                }

                // remove ? from prefix
                s = _qmkPrefixRegex.Replace(s, "$1");

                // remove ?
                bool dub = globalDub;
                if (s.Contains('?'))
                {
                    dub = true;
                    s = s.Replace("?", "");
                }

                // corner cases:
                // mid- > med.
                s = _midDashRegex.Replace(s, "med. $1");

                // later than the early: remove
                s = s.Replace("later than the early", "");

                // result complete
                results.Add(Tuple.Create(NormalizeWS(s), ca, dub));
            }

            return results;
        }

        public IList<HistoricalDate> Parse(string? text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<HistoricalDate>();

            _hints.Clear();

            // A1 preprocess for split
            string s = PreprocessForSplit(text);

            // A2 split dates, and for each date:
            foreach (string singleDate in SplitDates(s))
            {
                // B split date's datations
                IList<string> datations = SplitDatations(singleDate);

                // C1 preprocess date's datations
                foreach (var tad in PreprocessDatations(datations))
                {
                    // C2 check period first
                    HistoricalDate? d = MatchPeriod(tad.Item1);
                    if (d is not null)
                    {
                        // TODO
                    }
                    // else parse datation
                    else
                    {
                        // TODO
                    }
                }
            }
            throw new NotImplementedException();
        }
    }
}
