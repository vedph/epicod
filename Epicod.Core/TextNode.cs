namespace Epicod.Core
{
    /// <summary>
    /// A node for epigraphic texts scraped from a web resource.
    /// </summary>
    public class TextNode
    {
        /// <summary>
        /// Gets or sets the node identifier (1-N).
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the optional parent node identifier. This is 0 when
        /// there is no parent. In the database, 0 will be replaced by NULL.
        /// </summary>
        public int ParentId { get; set; }

        /// <summary>
        /// Gets or sets the corpus this node comes from.
        /// </summary>
        public string? Corpus { get; set; }

        /// <summary>
        /// Gets or sets the "Y", i.e. the node depth level (1-N).
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Gets or sets the "X", i.e. the sibling ordinal number (1-N).
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the node's name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the URI this node points to.
        /// </summary>
        public string? Uri { get; set; }

        /// <summary>
        /// Converts to string.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return $"[{Corpus}#{Id}] @{Y}.{X} {Name}";
        }
    }
}
