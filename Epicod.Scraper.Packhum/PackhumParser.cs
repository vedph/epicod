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
        private const int FLD_LOCATION = 2;
        private const int FLD_TYPE = 3;
        private const int FLD_DATE = 4;
        private const int FLD_REFERENCE = 5;
        private const int FLD_TAIL = 6;

        private readonly char[] _seps;
        private readonly Regex _typeRegex;
        private readonly Regex _writingRegex;
        private readonly Regex _refAbbrRegex;
        private readonly PackhumDateParser _dateParser;
        private readonly List<string> _refHeads;

        /// <summary>
        /// Gets or sets the optional logger.
        /// </summary>
        public ILogger? Logger { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PackhumParser"/> class.
        /// </summary>
        public PackhumParser()
        {
            _seps = new[] { '\u2014' };
            _dateParser = new PackhumDateParser();

            // type like [pottery]
            _typeRegex = new Regex(@"^\s*\[([^]]+)\]\s*$", RegexOptions.Compiled);

            // type as writing direction/layout
            _writingRegex = new Regex(
                @"^\s*(?:stoich|non-stoich|boustr|retr|retrogr)\.\s*\d*\s*$",
                RegexOptions.Compiled);

            // 2 initial capitals are usually a hint for SEG, IG, etc.
            _refAbbrRegex = new Regex(@"^\s*[A-Z]{2,}", RegexOptions.Compiled);
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

        private bool ParseType(string text, int nodeId,
            IList<TextNodeProperty> props)
        {
            Match m = _typeRegex.Match(text);
            if (m.Success)
            {
                props.Add(new TextNodeProperty(nodeId, TextNodeProps.TYPE,
                    m.Groups[1].Value));
                return true;
            }
            m = _writingRegex.Match(text);
            if (m.Success)
            {
                string v = text.Trim();
                if (v == "retr") v = "retrogr";
                props.Add(new TextNodeProperty(nodeId, TextNodeProps.LAYOUT, v));
                return true;
            }
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
            bool hasRegion = false, hasRef = false;

            for (int i = 0; i < tokens.Count; i++)
            {
                // reference
                if (StartsWithRef(tokens[i]))
                {
                    props.Add(new TextNodeProperty(
                        nodeId, TextNodeProps.REFERENCE, tokens[i]));
                    if (!hasRef)
                    {
                        hasRef = true;
                        field = FLD_REFERENCE + 1;
                    }
                    else field = FLD_TAIL;
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

                // location
                if (field <= FLD_LOCATION && !_refAbbrRegex.IsMatch(tokens[i]))
                {
                    props.Add(new TextNodeProperty(
                        nodeId,
                        hasRegion? TextNodeProps.LOCATION : TextNodeProps.REGION,
                        tokens[i]));
                    field = (hasRegion ? FLD_LOCATION : FLD_REGION) + 1;
                    hasRegion = true;
                    continue;
                }

                Logger?.LogError("Unknown field in {Note} at {Index}",
                    note, i + 1);
            }

            return props;
        }
    }
}
