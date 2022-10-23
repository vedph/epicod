using System.Collections.Generic;

namespace Epicod.Core
{
    /// <summary>
    /// The result of browsing or searching the set of text nodes with their
    /// properties.
    /// </summary>
    public class TextNodeResult : TextNode
    {
        /// <summary>
        /// Gets or sets a value indicating whether this node is expandable.
        /// </summary>
        public bool IsExpandable { get; set; }

        /// <summary>
        /// Gets or sets the node's properties.
        /// </summary>
        public List<TextNodeResultProperty>? Properties { get; set; }
    }
}
