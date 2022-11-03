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
            @"(?<m>later|lat\.|earlier|früher|später|aft\.|after)\??",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _earlyModifierRegex = new(
            ", early$", RegexOptions.Compiled);

        private static readonly Regex _dateSuffixRegex = new
            (@",\s*(?<d>[0-3]?[0-9])?\s*" +
             @"(?<m>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[^\s]*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] _dateSeps = new[]
        {
            " and ", " or ", " od.", " oder ", " vel ", " & ", ","
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

        private static readonly Regex _dmyRegex = new(
            @"(?<d>[1-3]?[0-9])\.(?<m>1?[0-9])\.(?<y>[0-9]+)",
            RegexOptions.Compiled);

        private static readonly Regex _bracketRegex = new(@"\([^)]*\)",
            RegexOptions.Compiled);

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
            "early|eher|late|" +
            "1st half|2nd half|1th ?Halfte|2th ?Halfte|" +
            @"1th Drittel|1st third of the|1st third of|1st third)\?",
            RegexOptions.Compiled);

        private static readonly Regex _midDashRegex = new(@"\bmid-([0-9])",
            RegexOptions.Compiled);

        private static readonly Regex _dotCenturyRegex = new(
            @"([0-9])\.(?: ?Jh\.)?", RegexOptions.Compiled);

        private static readonly Regex _dateRegex = new(
            @"^(?:(?<t>ante|post|bef\.|before|aft\.|after) )?" +
            @"(?<m>init\.|beg\.|Anf\.|med\.|middle|mid|fin\.|end|Ende|Wende|" +
            "early|eher|late|1st half|2nd half|1th ?Halfte|2th ?Halfte|" +
            "1th Drittel|1st third of the|1st third of|1st third)? " +
            @"?(?<c>s\.)? ?(?<n>[0-9IVX]+)" +
            @"(?:\/(?<ns>[0-9IVX]+))?(?<o>st|nd|rd|th)? ?" +
            @"(?<c>c\. )?" +
            @"(?<e>BC|ac|a\.|v\. ?Chr\.|AD|pc|p\.|n\. ?Chr\.)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly Dictionary<string, HistoricalDate?>
            _periods = new();
        #endregion

        /// <summary>
        /// Gets or sets the optional logger.
        /// </summary>
        public ILogger? Logger { get; set; }

        // state
        private readonly List<string> _hints;
        private short _month;
        private short _day;

        public PackhumDateParser()
        {
            _hints = new List<string>();
        }

        private static short GetMonthNumber(string month)
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
                if (i == 0) break;
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
            StringBuilder sb = new(s);
            sb.Replace("w//", "w");
            sb.Replace("w/", "w");
            sb.Replace("July/August", "July");
            sb.Replace("early/mid", "early");
            sb.Replace("half/mid", "1st half");
            sb.Replace("mid/2nd half of the", "mid");
            sb.Replace("middle / 2nd half", "mid");
            s = sb.ToString();

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

            // date suffix
            s = _dateSuffixRegex.Replace(s, (Match m) =>
            {
                _day = m.Groups["d"].Length > 0
                    ? short.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)
                    : (short)0;
                _month = GetMonthNumber(m.Groups[2].Value);
                return "";
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

        private void EnsurePeriodsLoaded()
        {
            if (_periods.Count != 0) return;

            using StreamReader reader = new(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(
                "Epicod.Scraper.Packhum.Assets.Periods.csv")!,
                Encoding.UTF8);

            string? line;
            HistoricalDate undefDate = new();
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] cols = line.Split(',');
                if (cols[1].Length > 0)
                {
                    _periods[cols[0].ToLowerInvariant()] =
                        HistoricalDate.Parse(cols[1])!;
                }
                else _periods[cols[0].ToLowerInvariant()] = undefDate;
            }
        }

        private HistoricalDate? MatchPeriod(string text)
        {
            EnsurePeriodsLoaded();

            string s = text.Replace("?", "").ToLowerInvariant();
            s = NormalizeWS(_bracketRegex.Replace(s, "")).Trim();
            if (!_periods.ContainsKey(s) || _periods[s] is null) return null;

            HistoricalDate date = _periods[s]!.Clone();
            if (date.GetDateType() != HistoricalDateType.Undefined &&
                text.IndexOf('?') > -1)
            {
                date.A.IsDubious = date.B!.IsDubious = true;
            }

            return date;
        }

        /// <summary>
        /// Preprocesses the specified datations.
        /// </summary>
        /// <param name="texts">The datation texts.</param>
        /// <returns>One tuple for each input text, with 1=preprocessed text,
        /// 2=about, 3=dubious.</returns>
        /// <exception cref="ArgumentNullException">text</exception>
        public IList<Tuple<string, bool, bool>> PreprocessDatations(
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

                // date DMY (hapax: 13.2.139 n.Chr.)
                m = _dmyRegex.Match(s);
                if (m.Success)
                {
                    if (m.Groups["m"].Length > 0)
                    {
                        _month = short.Parse(m.Groups["m"].Value,
                            CultureInfo.InvariantCulture);
                    }
                    if (m.Groups["d"].Length > 0)
                    {
                        _day = short.Parse(m.Groups["d"].Value,
                            CultureInfo.InvariantCulture);
                    }
                    s = s.Remove(m.Index, m.Length)
                         .Insert(m.Index, m.Groups["y"].Value);
                }

                // N. or N.Jh. = Nth + space
                s = _dotCenturyRegex.Replace(s, "$1th ").Replace("th /", "th/");

                // corner cases:
                // p. ante or p. post => ante or post
                s = s.Replace("p. ante ", "ante ");
                s = s.Replace("p. post ", "post ");

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
                case "late":
                    delta = 85;
                    break;
                case "1sthalf":
                case "1thhalfte":
                    delta = 25;
                    break;
                case "2ndhalf":
                case "2thhalfte":
                    delta = 75;
                    break;
                case "1thdrittel":
                case "1stthird":
                case "1stthirdof":
                case "1stthirdofthe":
                    delta = 17;
                    break;
            }

            if (d.Value < 0) delta = -(100 - delta);
            d.Value = n + delta;
            d.IsCentury = false;
            d.IsApproximate = true;
        }

        private void ApplyMonthDay(Datation d)
        {
            if (d.IsCentury)
            {
                StringBuilder sb = new();
                if (d.Hint != null) sb.Append(d.Hint).Append("; ");
                if (_month > 0) sb.Append("month=").Append(_month);
                if (_day > 0)
                {
                    if (_month > 0) sb.Append(", ");
                    sb.Append("day=").Append(_day);
                }
                d.Hint = sb.ToString();
            }
            else
            {
                d.Month = _month;
                d.Day = _day;
            }
        }

        private Datation? BuildDatation(
            Tuple<string, bool, bool> tad, bool prevBC, string? hint)
        {
            // t = ante/post
            // m = modifier (init. etc)
            // c = century (s.)
            // n = number (N or R)
            // ns = number suffix (/N or /R)
            // o = suffix (st, nd, etc)
            // e = era (BC etc)
            // era: inherit if not explicitly defined
            Match m = _dateRegex.Match(tad.Item1);
            if (!m.Success)
            {
                Logger?.LogError("Invalid datation: {Datation}", tad.Item1);
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
            if (_day > 0 || _month > 0) ApplyMonthDay(d);

            return d;
        }

        private static int GetTerminusType(string text)
        {
            if (text.StartsWith("ante ", StringComparison.Ordinal) ||
                text.StartsWith("bef.", StringComparison.Ordinal) ||
                text.StartsWith("before ", StringComparison.Ordinal))
            {
                return -1;
            }
            if (text.StartsWith("post ", StringComparison.Ordinal) ||
                text.StartsWith("aft.", StringComparison.Ordinal) ||
                text.StartsWith("after", StringComparison.Ordinal))
            {
                return 1;
            }
            return 0;
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
            _month = _day = 0;

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
                    if (period.GetDateType() != HistoricalDateType.Undefined)
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

                // C2 parse datations (either 2 or 1)
                HistoricalDate date = new();
                int t;
                if (datations.Count == 2)
                {
                    // build 2 datations points
                    // B
                    Datation? b = BuildDatation(tads[1], prevBC, hint);
                    // (no test for ante/post required in B, as ante/post
                    // only occur at the beginning of a date)
                    // A
                    Datation? a = BuildDatation(tads[0],
                        b?.Value < 0 || prevBC, hint);
                    // should A be a terminus ante/post, refactor AB into
                    // a point and make it B or A
                    t = GetTerminusType(tads[0].Item1);
                    if (t != 0)
                    {
                        if (a != null) date.SetStartPoint(a);
                        if (b != null) date.SetEndPoint(b);
                        int n = (int)date.GetSortValue();
                        if (t < 0)
                        {
                            date.Reset();
                            date.SetEndPoint(new Datation
                            {
                                Value = n,
                                IsApproximate = true,
                                IsDubious = tads[0].Item3,
                                Hint = hint
                            });
                        }
                        else
                        {
                            date.Reset();
                            date.SetStartPoint(new Datation
                            {
                                Value = n,
                                IsApproximate = true,
                                IsDubious = tads[0].Item3,
                                Hint = hint
                            });
                        }
                    }
                    else
                    {
                        if (a != null) date.SetStartPoint(a);
                        if (b != null) date.SetEndPoint(b);
                    }
                    prevBC = a?.Value < 0 || b?.Value < 0;
                }
                else
                {
                    // build single datation point
                    Datation? d = BuildDatation(tads[0], prevBC, hint);
                    if (d == null)
                    {
                        Logger?.LogError("Invalid datation: {Datation}",
                            tads[0].Item1);
                        continue;
                    }
                    // it's a range if ante/post, else it's a point
                    t = GetTerminusType(tads[0].Item1);
                    if (t != 0)
                    {
                        if (t == -1) date.SetEndPoint(d);
                        else date.SetStartPoint(d);
                    }
                    else date.SetSinglePoint(d);
                    prevBC = d.Value < 0;
                }

                // remove the first of two equal hints in a range
                if (date.GetDateType() == HistoricalDateType.Range &&
                    date.A.Hint != null && date.B!.Hint == date.A.Hint)
                {
                    date.A.Hint = null;
                }

                dates.Insert(0, date);
            }
            return dates;
        }
    }
}
