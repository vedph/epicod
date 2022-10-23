using Fusi.Tools.Data;

namespace Epicod.Core
{
    /// <summary>
    /// A filter for nodes used by <see cref="ICorpusBrowser"/>.
    /// </summary>
    /// <seealso cref="PagingOptions" />
    public class TextNodeFilter : PagingOptions
    {
        /// <summary>
        /// Gets or sets the corpus identifier.
        /// </summary>
        public string? CorpusId { get; set; }

        /// <summary>
        /// Gets or sets the optional parent node identifier.
        /// </summary>
        public int ParentId { get; set; }
    }
}
