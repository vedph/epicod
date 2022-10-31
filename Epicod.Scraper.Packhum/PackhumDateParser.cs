using Fusi.Antiquity.Chronology;
using Fusi.Tools;
using Microsoft.Extensions.Logging;
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
            @"([0-9IVX](?:st|nd|rd|th)?\??)-([0-9IVX])", RegexOptions.Compiled);
        private static readonly Regex _splitPtSlashRegex = new(
            @"([IVX]\??)/([0-9IVX])", RegexOptions.Compiled);
        private static readonly Regex _splitPtDashRegex = new(
            @"([0-9](?:\.|st|nd|rd|th)\??)/([0-9])", RegexOptions.Compiled);

        private static readonly Regex _qmkPrefixRegex = new(
            "(init.|beg.|Anf.|med.|mid|middle|" +
            "fin.|end|Ende|Wende|" +
            @"early|eher|early\s*/\s*mid|late|" +
            @"1st half|2nd half|1\.\s*Halfte|2\.\s*Halfte|" +
            "1th Drittel|1st third of the|1st third of|1st third|" +
            @"mid\s*/\s*2nd half|middle\s*/\s*2nd half)\?",
            RegexOptions.Compiled);

        private static readonly Regex _midDashRegex = new(@"\bmid-([0-9])",
            RegexOptions.Compiled);

        private static readonly Regex _dotCenturyRegex = new(
            @"([0-9])\.(?: ?Jh\.)?", RegexOptions.Compiled);

        private static readonly Regex _macroRegex = new(
            @"\{([^}]+)\}", RegexOptions.Compiled);

        private static readonly Regex _macroArgRegex = new(
            "(?<n>[^=]+)=(?<v>[^,]*)", RegexOptions.Compiled);

        private static readonly Regex _dateRegex = new(
            "^(?<t>ante |post )?" +
            @"(?<m>init\.|beg\.|Anf\.|med\.|middle|mid|fin\.|end|Ende|Wende|" +
            @"early|eher|early*\/ *mid|late|1st half|2nd half|1\. ?Halfte|2\. ?" +
            "Halfte|1th Drittel|1st third of the|1st third of|1st third|" +
            @"mid\s*/\s*2nd half)? " +
            @"?(?<c>s\.)? ?(?<n>[0-9IVX]+)" +
            @"(?:\/(?<ns>[0-9IVX]+))?(?<o>st|nd|rd|th)? ?" +
            @"(?<e>BC|ac|a\.|v\. ?Chr\.|AD|pc|p\.|n\. ?Chr\.)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Dictionary<string, HistoricalDate>
            _periods = new();
        #endregion

        /// <summary>
        /// Gets or sets the optional logger.
        /// </summary>
        public ILogger? Logger { get; set; }

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
                _hints.Add(text[(i + 1)..end]);
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
            if (!r.IsMatch(text)) return new[] { text };

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

            if (text.Length == 0) return Array.Empty<string>();

            if (_splitPtRegex.IsMatch(text))
                return SplitAtRegexWithSep(text, _splitPtRegex);

            if (_splitPtDashRegex.IsMatch(text))
                return SplitAtRegexWithSep(text, _splitPtDashRegex);

            return SplitAtRegexWithSep(text, _splitPtSlashRegex);
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
            string s = NormalizeWS(text).Replace("?", "").ToLowerInvariant();
            if (!_periods.ContainsKey(s)) return null;

            HistoricalDate date = _periods[s].Clone();
            if (text.IndexOf('?') > -1)
                date.A.IsDubious = date.B!.IsDubious = true;
            return date;
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

                // N. or N.Jh. = Nth + space
                s = _dotCenturyRegex.Replace(s, "$1th ").Replace("th /", "th/");

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

        /// <summary>
        /// Parses a single value of a date, in the form N or R, representing
        /// a year or a century.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>Value.</returns>
        private static int ParseDateValue(string text)
        {
            return text[0] >= '0' && text[0] <= '9'
                ? int.Parse(text, CultureInfo.InvariantCulture)
                : RomanNumber.FromRoman(text.ToUpperInvariant());
        }

        private static Tuple<int, int> ParseDateValues(string a, string? b,
            bool bc)
        {
            int na = ParseDateValue(a);
            int nb = string.IsNullOrEmpty(b) ? 0 : ParseDateValue(b);
            if (bc || (nb > 0 && na > nb))
            {
                na = -na;
                if (nb > 0) nb = -nb;
            }
            return Tuple.Create(na, nb);
        }

        private static bool IsBC(string era, bool defaultValue)
        {
            return era.ToLowerInvariant() switch
            {
                "bc" => true,
                "ac" => true,
                "a." => true,
                "v.chr." => true,
                "v. chr." => true,
                _ => defaultValue,
            };
        }

        private static int CenturySpanToN(int a, int b)
        {
            if (a > b) (b, a) = (a, b);
            return ((a - 1) * 100) + ((a - b) / 2);
        }

        private static HistoricalDate BuildTerminusDate(Match m, int a, int b,
            bool century, string? hint)
        {
            HistoricalDate date = new();

            // /R or /N being century is a range, so we must
            // refactor this value into a point
            if (m.Groups["ns"].Length > 0 && century)
            {
                if (string.Equals(m.Groups["t"].Value, "ante",
                    StringComparison.InvariantCultureIgnoreCase))
                {
                    date.SetEndPoint(new Datation
                    {
                        Value = CenturySpanToN(a, b),
                        Hint = hint
                    });
                }
                else
                {
                    date.SetStartPoint(new Datation
                    {
                        Value = CenturySpanToN(a, b),
                        Hint = hint
                    });
                }
            }
            // else we have a simple terminus ante/post
            else if (string.Equals(m.Groups["t"].Value, "ante",
                    StringComparison.InvariantCultureIgnoreCase))
            {
                date.SetEndPoint(new Datation
                {
                    Value = a,
                    IsCentury = century,
                    IsSpan = m.Groups["ns"].Length > 0,
                    Hint = hint
                });
            }
            else
            {
                date.SetStartPoint(new Datation
                {
                    Value = b,
                    IsCentury = century,
                    IsSpan = m.Groups["ns"].Length > 0,
                    Hint = hint
                });
            }

            return date;
        }

        private string? BuildHint() =>
            _hints.Count == 0 ? null : string.Join("; ", _hints);

        private static void ApplyDateModifier(Datation d, string m)
        {
            // modifiers can be applied only to centuries
            if (!d.IsCentury) return;

            // year value: e.g. IV BC=-300, IV AD=300
            int n = (d.Value < 0 ? (d.Value + 1) : (d.Value - 1)) * 100;

            int delta = 0;
            switch (m.ToLowerInvariant().Replace(" ", ""))
            {
                case "init.":
                case "beg.":
                case "anf.":
                    delta = 10;
                    break;
                case "med.":
                case "mid":
                case "middle":
                    delta = 50;
                    break;
                case "fin.":
                case "end":
                case "ende":
                case "wende":
                    delta = 90;
                    break;
                case "early":
                case "eher":
                    delta = 15;
                    break;
                case "early/mid":
                    delta = 20;
                    break;
                case "late":
                    delta = 85;
                    break;
                case "1sthalf":
                case "1.halfte":
                    delta = 25;
                    break;
                case "2ndhalf":
                case "2.halfte":
                    delta = 75;
                    break;
                case "1thdrittel":
                case "1stthird":
                case "1stthirdof":
                case "1stthirdofthe":
                    delta = 17;
                    break;
                case "mid/2ndhalf":
                case "middle/2ndhalf":
                    delta = 40;
                    break;
            }

            d.Value = n + delta;
            d.IsCentury = false;
            d.IsApproximate = true;
        }

        private static Tuple<string, IDictionary<string,string>>? ExtractMacro(
            string text)
        {
            Match m = _macroRegex.Match(text);
            if (!m.Success) return null;

            IDictionary<string, string> dct = new Dictionary<string, string>();
            foreach (Match pair in _macroArgRegex.Matches(m.Groups[1].Value))
                dct[pair.Groups["n"].Value] = pair.Groups["v"].Value;

            return Tuple.Create(text[m.Index..(m.Index + m.Length)], dct);
        }

        private static void ApplyMonthDay(Datation d, short month, short day)
        {
            if (d.IsCentury)
            {
                StringBuilder sb = new();
                if (d.Hint != null) sb.Append(d.Hint).Append("; ");
                if (month > 0) sb.Append("month=").Append(month);
                if (day > 0)
                {
                    if (month > 0) sb.Append(", ");
                    sb.Append("day=").Append(day);
                }
                d.Hint = sb.ToString();
            }
            else
            {
                d.Month = month;
                d.Day = day;
            }
        }

        private Datation? BuildDatation(Tuple<string, bool, bool> tad,
            bool prevBC, string? hint)
        {
            short day = 0, month = 0;
            string text = tad.Item1;
            Tuple<string, IDictionary<string, string>>? td = ExtractMacro(text);

            if (td != null)
            {
                text = td.Item1;
                day = td.Item2.ContainsKey("day")
                    ? short.Parse(td.Item2["day"], CultureInfo.InvariantCulture)
                    : (short)0;
                month = td.Item2.ContainsKey("month")
                    ? short.Parse(td.Item2["month"], CultureInfo.InvariantCulture)
                    : (short)0;
            }

            // t = ante/post
            // m = modifier (init. etc)
            // c = century (s.)
            // n = number (N or R)
            // ns = number suffix (/N or /R)
            // o = suffix (st, nd, etc)
            // e = era (BC etc)
            // era: inherit if not explicitly defined
            Match m = _dateRegex.Match(text);
            if (!m.Success)
            {
                Logger?.LogError("Invalid datation: {Datation}", text);
                return null;
            }
            bool bc = IsBC(m.Groups["e"].Value, prevBC);

            // century if s. or ordinal suffix or Roman digits
            bool century = m.Groups["c"].Length > 0
                || m.Groups["o"].Length > 0
                || !char.IsDigit(m.Groups["n"].Value[0]);

            // value
            string a = m.Groups["n"].Value;
            string? b = century? m.Groups["ns"].Value : null;
            Tuple<int, int> ab = ParseDateValues(a, b, bc);

            // if ante/post, it's a range; in this case, century
            // is refactored as N (e.g. IV-III BC = -250) so we get a point
            if (m.Groups["t"].Length > 0)
            {
                HistoricalDate date = BuildTerminusDate(m, ab.Item1, ab.Item2,
                    century, hint);
                date.A.IsApproximate = tad.Item2;
                date.A.IsDubious = tad.Item3;
                // apply modifiers if any
                if (m.Groups["m"].Length > 0)
                    ApplyDateModifier(date.A, m.Groups["m"].Value);
                return date.A;
            }

            Datation d = new()
            {
                Value = ab.Item1,
                IsCentury = century,
                IsSpan = m.Groups["ns"].Length > 0,
                Hint = hint,
                IsApproximate = tad.Item2,
                IsDubious = tad.Item3,
            };

            // apply modifiers if any
            if (m.Groups["m"].Length > 0)
                ApplyDateModifier(d, m.Groups["m"].Value);

            // apply day & month if any
            if (day > 0 || month > 0) ApplyMonthDay(d, month, day);

            return d;
        }

        /// <summary>
        /// Parses the specified text representing PHI date(s).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>Date(s).</returns>
        public IList<HistoricalDate> Parse(string? text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<HistoricalDate>();

            _hints.Clear();

            // A1 preprocess for split
            string s = PreprocessForSplit(text);

            // A2 split dates
            List<HistoricalDate> dates = new();
            bool prevBC = false;
            string? hint = BuildHint();

            // for each date (in reverse order so that we can supply metadata
            // from the last to the previous ones)
            foreach (string dateText in SplitDates(s).Reverse())
            {
                HistoricalDate? period = MatchPeriod(dateText);
                if (period is not null)
                {
                    dates.Insert(0, period);
                    continue;
                }

                // B split date into 0-2 datations
                IList<string> datations = SplitDatations(dateText);
                if (datations.Count < 1 || datations.Count > 2)
                {
                    Logger?.LogError("Unexpected number of datations: {Datations}",
                        string.Join("; ", datations));
                    continue;
                }

                // C1 preprocess datations
                IList<Tuple<string, bool, bool>> tads =
                    PreprocessDatations(datations);

                // C2 parse datations
                HistoricalDate date = new();
                if (datations.Count == 2)
                {
                    Datation? b = BuildDatation(tads[1], prevBC, hint);
                    Datation? a = BuildDatation(
                        tads[0], b?.Value < 0 || prevBC, hint);

                    if (a != null) date.SetStartPoint(a);
                    if (b != null) date.SetEndPoint(b);
                    prevBC = a?.Value < 0 || b?.Value < 0;
                }
                else
                {
                    Datation? a = BuildDatation(tads[0], prevBC, hint);
                    if (a == null)
                    {
                        Logger?.LogError("Invalid datation: {Datation}",
                            tads[0].Item1);
                        continue;
                    }
                    date.SetSinglePoint(a);
                    prevBC = a.Value < 0;
                }
                dates.Insert(0, date);
            }
            return dates;
        }
    }
}
