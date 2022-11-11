using Epicod.Core;
using Fusi.Antiquity.Chronology;
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
    /// A simple parser for a Packhum text note.
    /// </summary>
    public sealed class PackhumParser
    {
        private const int FLD_REGION = 1;
        private const int FLD_SITE = 2;
        private const int FLD_LOCATION = 3;
        private const int FLD_TYPE = 4;
        private const int FLD_DATE = 5;
        private const int FLD_REFERENCE = 6;
        private const int FLD_TAIL = 7;

        private static readonly char[] _seps = new[] { '\u2014' };

        private static readonly Regex _layoutRegex = new (
            @"^\s*(?:stoich|non-stoich|boustr|bstr|retr|retrogr|sinistr)\b",
            RegexOptions.Compiled);

        private static readonly Regex _forgeryRegex = new(
            @"(?<r2>probable )?(modern )?forgery(?<r3>\?)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _retrogrRegex = new(
            @"(?<t>retr|retrogr)\.\s*(?<q>\?)?",
            RegexOptions.Compiled);

        private static readonly Regex _boustrRegex = new(
            @"(?<t>bstr|boustr)\.\s*(?<q>\?)?",
            RegexOptions.Compiled);

        private static readonly Regex _stoichRegex = new(
            @"(?<t>stoich|non-stoich)\.\s*(?<c>c\.\s*)?" +
            "(?:(?<n1>[0-9]+)(?:[-/](?<n2>[0-9]+))?)?",
            RegexOptions.Compiled);

        // state
        private readonly PackhumDateParser _dateParser;
        private readonly List<string> _refHeads;

        private static readonly string[] RSL = new[]
            {
                TextNodeProps.REGION, TextNodeProps.SITE, TextNodeProps.LOCATION
            };

        /// <summary>
        /// Gets or sets the optional logger.
        /// </summary>
        public ILogger? Logger { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PackhumParser"/> class.
        /// </summary>
        public PackhumParser()
        {
            _dateParser = new PackhumDateParser();

            // type as writing direction/layout
            _refHeads = new();
            LoadRefHeads();
        }

        private void LoadRefHeads()
        {
            _refHeads.Clear();
            using StreamReader reader = new(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(
                "Epicod.Scraper.Packhum.Assets.RefHeads.txt")!, Encoding.UTF8);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrEmpty(line)) _refHeads.Add(line);
            }
        }

        private bool StartsWithRef(string? text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < _refHeads.Count; i++)
            {
                if (_refHeads[i].Length <= text.Length &&
                    text.StartsWith(_refHeads[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        public static int ParseForgery(string? text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            Match m = _forgeryRegex.Match(text);
            if (m.Success)
            {
                return m.Groups["r3"].Length > 0
                    ? 3
                    : (m.Groups["r2"].Length > 0 ? 2 : 1);
            }
            return 0;
        }

        private static bool AddForgeryProp(string text, int nodeId,
            IList<TextNodeProperty> props)
        {
            int n = ParseForgery(text);
            if (n > 0)
            {
                string v = n.ToString(CultureInfo.InvariantCulture);
                if (props.All(p => p.Name != TextNodeProps.FORGERY &&
                    p.Value != v))
                {
                    props.Add(new TextNodeProperty(nodeId,
                        TextNodeProps.FORGERY,
                        v,
                        TextNodeProps.TYPE_INT));
                }
                return true;
            }
            return false;
        }

        private static void ParseRetrogr(string text, int nodeId,
            IList<TextNodeProperty> props)
        {
            Match m = _retrogrRegex.Match(text);
            if (!m.Success) return;

            props.Add(new TextNodeProperty(nodeId, TextNodeProps.RTL,
                m.Groups["q"].Length > 0 ? "2" : "1", TextNodeProps.TYPE_INT));
        }

        private static void ParseBoustr(string text, int nodeId,
            IList<TextNodeProperty> props)
        {
            Match m = _boustrRegex.Match(text);
            if (!m.Success) return;

            props.Add(new TextNodeProperty(nodeId, TextNodeProps.BOUSTR,
                m.Groups["q"].Length > 0 ? "2" : "1", TextNodeProps.TYPE_INT));
        }

        private static void ParseStoich(string text, int nodeId,
            IList<TextNodeProperty> props)
        {
            Match m = _stoichRegex.Match(text);
            if (!m.Success) return;

            // t = stoich/non-stoich
            bool non =
                m.Groups["t"].Value.StartsWith("non", StringComparison.Ordinal);

            // n1: if missing stoich=0,0; if non-stoich, just ignore
            int n1;
            if (m.Groups["n1"].Length == 0)
            {
                if (non) return;
                n1 = 0;
            }
            else
            {
                n1 = int.Parse(m.Groups["n1"].Value, CultureInfo.InvariantCulture);
            }

            // n2: equal to n1 if missing
            int n2 = n1 == 0
                ? 0
                : m.Groups["n2"].Length > 0
                    ? int.Parse(m.Groups["n2"].Value, CultureInfo.InvariantCulture)
                    : n1;

            // ensure that n1 is min and n2 is max
            if (n1 > n2) (n1, n2) = (n2, n1);

            // add min/max
            if (non)
            {
                props.Add(new TextNodeProperty(nodeId, TextNodeProps.NON_STOICH_MIN,
                    $"{n1}", TextNodeProps.TYPE_INT));
                props.Add(new TextNodeProperty(nodeId, TextNodeProps.NON_STOICH_MAX,
                    $"{n2}", TextNodeProps.TYPE_INT));
            }
            else
            {
                props.Add(new TextNodeProperty(nodeId, TextNodeProps.STOICH_MIN,
                    $"{n1}", TextNodeProps.TYPE_INT));
                props.Add(new TextNodeProperty(nodeId, TextNodeProps.STOICH_MAX,
                    $"{n2}", TextNodeProps.TYPE_INT));
            }
        }

        private static bool ParseType(string text, int nodeId,
            IList<TextNodeProperty> props)
        {
            if (text.Length > 2 && text[0] == '[' && text[^1] == ']')
            {
                props.Add(new TextNodeProperty(nodeId, TextNodeProps.TYPE,
                    text[1..^1]));
                return true;
            }

            // layout
            Match m = _layoutRegex.Match(text);
            if (m.Success)
            {
                string v = text.Trim();
                if (v == "retr") v = "retrogr";
                props.Add(new TextNodeProperty(nodeId, TextNodeProps.LAYOUT, v));

                // boustr.
                ParseBoustr(text, nodeId, props);

                // retrogr.
                ParseRetrogr(text, nodeId, props);

                // stoich.
                ParseStoich(text, nodeId, props);

                return true;
            }

            // forgery
            if (AddForgeryProp(text, nodeId, props)) return true;

            return false;
        }

        private bool ParseDate(string text, int nodeId,
            IList<TextNodeProperty> props)
        {
            IList<HistoricalDate> dates = _dateParser.Parse(text);
            int n = 0;

            foreach (HistoricalDate date in dates)
            {
                // all the alternative dates after the 1st are suffixed
                // (e.g. "date-txt#1", "date-val#1", "date-txt#2"...)
                string suffix = ++n > 1? $"#{n}" : "";

                props.Add(new TextNodeProperty(nodeId,
                    TextNodeProps.DATE_TXT + suffix,
                    date.ToString()));

                props.Add(new TextNodeProperty(nodeId,
                    TextNodeProps.DATE_VAL + suffix,
                    date.GetSortValue().ToString(CultureInfo.InvariantCulture),
                    TextNodeProps.TYPE_INT));
            }

            return dates.Count > 0;
        }

        /// <summary>
        /// Parses the specified note.
        /// </summary>
        /// <param name="note">The note.</param>
        /// <param name="nodeId">The node identifier.</param>
        /// <returns>Properties parsed from note.</returns>
        public IList<TextNodeProperty> ParseNote(string note, int nodeId)
        {
            if (string.IsNullOrEmpty(note)) return Array.Empty<TextNodeProperty>();

            List<string> tokens = note.Split(_seps).Select(s => s.Trim()).ToList();
            IList<TextNodeProperty> props = new List<TextNodeProperty>();
            int field = 0;
            int place = 0;
            bool hasRef = false;

            // forgery may be found in many fields like e.g.
            // "ca. 800 BC? (forgery?)", "cf. SEG 24.1252 (forgery)", etc.
            AddForgeryProp(note, nodeId, props);

            for (int i = 0; i < tokens.Count; i++)
            {
                // reference
                if (field == FLD_TAIL || StartsWithRef(tokens[i]))
                {
                    props.Add(new TextNodeProperty(
                        nodeId, TextNodeProps.REFERENCE, tokens[i]));
                    if (field != FLD_TAIL)
                    {
                        if (!hasRef)
                        {
                            hasRef = true;
                            field = FLD_REFERENCE + 1;
                        }
                        else field = FLD_TAIL;
                    }
                    continue;
                }

                // type
                if (field <= FLD_TYPE && ParseType(tokens[i], nodeId, props))
                {
                    field = FLD_TYPE + 1;
                    continue;
                }

                // date
                if (field <= FLD_DATE && ParseDate(tokens[i], nodeId, props))
                {
                    props.Add(new TextNodeProperty(nodeId,
                        TextNodeProps.DATE_PHI, tokens[i]));
                    continue;
                }

                // region/site/location
                if (field <= FLD_LOCATION)
                {
                    // 1=region, 2=site, 3=location
                    place++;
                    props.Add(new TextNodeProperty(
                        nodeId,
                        RSL[place - 1],
                        tokens[i]));
                    switch (place)
                    {
                        case 1:
                            field = FLD_REGION;
                            break;
                        case 2:
                            field = FLD_SITE;
                            break;
                        case 3:
                            field = FLD_LOCATION;
                            break;
                    }
                    field++;
                    continue;
                }

                Logger?.LogError("Unknown field {Number} ({Field}) in {Note}",
                    i + 1, tokens[i], note);
            }

            return props;
        }
    }
}
