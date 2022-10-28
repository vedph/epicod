using Fusi.Antiquity.Chronology;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Epicod.Scraper.Packhum
{
    public sealed class PackhumDateParser
    {
        private readonly Regex _wsRegex;
        private readonly Regex _orModifierRegex;
        private readonly Regex _earlyModifierRegex;
        private readonly Regex _dateSuffixRegex;
        private readonly string[] _dateSeps;

        private readonly Regex _apSuffixRegex;
        private readonly Regex _caRegex;
        private readonly Regex _splitRegex;

        // state
        private bool _globalCa;
        private bool _globalDub;
        private readonly List<string> _hints;

        public PackhumDateParser()
        {
            // preprocessing for split
            _wsRegex = new Regex(@"\s+", RegexOptions.Compiled);

            _orModifierRegex = new Regex(
                @"(?:or |od\.|oder )" +
                @"(?<l>shortly |slightly |sh\.)?" +
                @"(?<m>later|lat\.|after|aft.|früher|später)\??",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            _earlyModifierRegex = new Regex(", early$", RegexOptions.Compiled);

            _dateSuffixRegex = new Regex(@",\s*(?<d>[0-3]?[0-9])?\s*" +
                @"(?<m>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[^\s]*",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            _dateSeps = new[] { " and ", " or ", " od.", " oder ", " & ", "," };

            // date preprocessing
            _apSuffixRegex = new Regex(@"([0-9])(a\.|p\.)", RegexOptions.Compiled);
            _caRegex = new Regex(@"^ca?\.", RegexOptions.Compiled);
            _splitRegex = new Regex(@"[0-9]\?\s+[0-9]", RegexOptions.Compiled);

            _hints = new List<string>();
        }

        private static int GetMonthNumber(string month)
        {
            return month[3..].ToLowerInvariant() switch
            {
                "jan" => 1,
                "feb" => 2,
                "mar" => 3,
                "apr" => 4,
                "may" => 5,
                "jun" => 6,
                "jul" => 7,
                "ago" => 8,
                "sep" => 9,
                "oct" => 10,
                "nov" => 11,
                "dec" => 12,
                _ => 0,
            };
        }

        private string NormalizeWS(string s) => _wsRegex.Replace(s, " ").Trim();

        private string PreprocessForSplit(string text)
        {
            // normalize WS
            string t = NormalizeWS(text);

            // or...: wrap in ()
            t = _orModifierRegex.Replace(t, (Match m) => "(" + m.Value + ")");

            // , early... : wrap in 
            t = _earlyModifierRegex.Replace(t, (Match m) => "(" + m.Value + ")");

            // date suffix: wrap in {d=N,m=N} ({} are never used in PHI dates)
            t = _dateSuffixRegex.Replace(t, (Match m) =>
            {
                int day = m.Groups["d"].Length > 0
                    ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)
                    : 0;
                int month = GetMonthNumber(m.Groups[2].Value);
                return "{" + (day > 0 ? $"d={day},m={month}" : $"m={month}") + "}";
            });

            return t;
        }

        private IList<string> SplitDates(string text)
        {
            string s = PreprocessForSplit(text);

            // corner case: split at regex
            if (_splitRegex.IsMatch(text))
            {
                int i = text.LastIndexOf('?');
                Debug.Assert(i > -1);
                string tail = text[(i + 1)..];
                return (from split in _splitRegex.Split(s)
                        select split + tail).ToList();
            }

            // normal split
            return (from split in
                s.Split(_dateSeps, StringSplitOptions.RemoveEmptyEntries)
                select NormalizeWS(split))
                .ToList();
        }

        private string PreprocessDatation(string text)
        {
            // detach a./p. when attached to N
            string s = _apSuffixRegex.Replace(text, "$1 $2");

            // remove initial ca. (this must be applied to any datations)
            Match m = _caRegex.Match(s);
            if (m.Success)
            {
                _globalCa = true;
                s = s.Remove(0, m.Length);
            }

            // remove final ?
            if (s.Length > 0 && s[^1] == '?')
            {
                _globalDub = true;
                s = s[..^1];
            }

            return s;
        }

        private void Reset()
        {
            _globalCa = false;
            _globalDub = false;
            _hints.Clear();
        }

        public IList<HistoricalDate> Parse(string? text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<HistoricalDate>();

            Reset();
            foreach (string s in SplitDates(text))
            {
                // TODO
            }
            throw new NotImplementedException();
        }
    }
}
