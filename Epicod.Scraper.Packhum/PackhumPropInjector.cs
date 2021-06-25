using Fusi.Tools;
using Npgsql;
using SqlKata;
using SqlKata.Compilers;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Epicod.Scraper.Packhum
{
    /// <summary>
    /// Packhum properties injector.
    /// </summary>
    public sealed class PackhumPropInjector
    {
        private readonly string _connString;
        private readonly string[] _names;

        public PackhumPropInjector(string connString)
        {
            _connString = connString ??
                throw new ArgumentNullException(nameof(connString));
            _names = new[]
            {
                "region", "location", "type", "layout",
                "date-phi", "date-txt", "date-val", "date-nan",
                "reference"
            };
        }

        private void Clear(QueryFactory queryFactory)
        {
            queryFactory.Query("textnodeproperty")
                .Join("textnode", "textnode.id", "textnodeproperty.nodeid")
                .Where("corpus", PackhumScraper.CORPUS).AsDelete();
        }

        public int Inject(CancellationToken cancel,
            IProgress<ProgressReport> progress = null)
        {
            QueryFactory queryFactory = new QueryFactory(
                new NpgsqlConnection(_connString),
                new PostgresCompiler());

            Clear(queryFactory);

            PackhumNoteParser parser = new PackhumNoteParser();

            foreach (var item in queryFactory.Query("textnodeproperty")
                .Select("textnode.id as NodeId", "textnodeproperty.Note")
                .Join("textnode", "textnode.id", "textnodeproperty.nodeid")
                .Where("corpus", PackhumScraper.CORPUS)
                .OrderBy("textnode.id").Get())
            {
                IList<TextNodeProperty> props = parser.Parse
                    (item.Note, item.NodeId);
                // TODO
            }

            throw new NotImplementedException();
        }
    }
}
