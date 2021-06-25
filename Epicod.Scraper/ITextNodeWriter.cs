using System.Collections.Generic;

namespace Epicod.Scraper
{
    /// <summary>
    /// A writer for <see cref="TextNode"/> and their properties.
    /// </summary>
    public interface ITextNodeWriter
    {
        /// <summary>
        /// Writes the specified node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="properties">The optional node properties.</param>
        void Write(TextNode node, IList<TextNodeProperty> properties = null);
    }
}
