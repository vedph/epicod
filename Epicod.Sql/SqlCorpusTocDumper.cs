using Epicod.Core;
using Epicod.Scraper.Sql;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Epicod.Sql
{
    public sealed class SqlCorpusTocDumper : SqlCorpusBase
    {
        private string _corpus;
        private int _maxY;
        private IList<string> _properties;
        private CancellationToken _cancel;
        private IProgress<int> _progress;
        private int _count;

        public SqlCorpusTocDumper(string connString) : base(connString)
        {
        }

        private void DumpNode(dynamic d, TextWriter writer)
        {
            _count++;
            if (++_count % 10 == 0) _progress?.Report(_count);

            // y.x at y-th column
            int y = d.y;
            int i = 1;
            while (i < y)
            {
                writer.Write('\t');
                i++;
            }
            writer.Write($"{d.y}.{d.x}");
            i++;
            while (i < _maxY)
            {
                writer.Write('\t');
                i++;
            }

            // id,pid,name,uri
            writer.Write($"{d.id}\t{d.parentid}\t{d.name}\t{d.uri}");

            // node properties
            if (_properties?.Count > 0)
            {
                TextNodeResult node = DynamicToTextNode(d);
                LoadProperties(node, _properties);
                StringBuilder sb = new StringBuilder();

                foreach (string pn in _properties)
                {
                    sb.Clear();
                    sb.Append('\t');
                    int n = 0;
                    foreach (var prop in node.Properties.Where(p => p.Name == pn))
                    {
                        if (++n > 1) sb.Append(" | ");
                        sb.Append(prop.Value);
                    }
                    writer.Write(sb.ToString());
                }
            }
            writer.WriteLine();

            // children
            foreach (var child in _qf.Query(EpicodSchema.T_NODE)
                .Select("id", "parentid", "y", "x", "name", "uri")
                .Where("corpus", _corpus)
                .Where("parentid", d.id).OrderBy("x").Get())
            {
                DumpNode(child, writer);
            }
        }

        public void Dump(string corpus,
            IList<string> properties,
            TextWriter writer,
            CancellationToken cancel,
            IProgress<int> progress = null)
        {
            if (corpus is null) throw new ArgumentNullException(nameof(corpus));
            if (writer is null) throw new ArgumentNullException(nameof(writer));

            EnsureQueryFactory();

            _corpus = corpus;
            _properties = properties;
            _cancel = cancel;
            _progress = progress;
            _count = 0;

            // (a) header
            _maxY = (int)_qf.Query(EpicodSchema.T_NODE)
                .SelectRaw("MAX(y) AS maxy")
                .Where("corpus", _corpus)
                .First().maxy;

            // y1...N
            for (int n = 1; n <= _maxY; n++)
            {
                if (n > 1) writer.Write('\t');
                writer.Write($"y{n}");
            }

            // node fields
            writer.Write("id\tpid\tname\turi");

            // node properties
            if (properties?.Count > 0)
            {
                foreach (string p in properties) writer.Write($"\t{p}");
            }

            writer.WriteLine();

            // (b) data
            foreach (var d in _qf.Query(EpicodSchema.T_NODE)
                .Select("id", "parentid", "y", "x", "name", "uri")
                .Where("corpus", corpus)
                .Where("y", 1)
                .OrderBy("x").Get())
            {
                DumpNode(d, writer);
            }
        }
    }
}
