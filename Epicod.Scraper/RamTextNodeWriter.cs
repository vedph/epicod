using Epicod.Core;
using System;
using System.Collections.Generic;

namespace Epicod.Scraper
{
    public class RamTextNodeWriter : ITextNodeWriter
    {
        public IList<TextNode> Nodes { get; }
        public IDictionary<int, IList<TextNodeProperty>> Properties { get; }

        public RamTextNodeWriter()
        {
            Nodes = new List<TextNode>();
            Properties = new Dictionary<int, IList<TextNodeProperty>>();
        }

        public void Write(TextNode node, IList<TextNodeProperty>? properties = null)
        {
            if (node is null) throw new ArgumentNullException(nameof(node));

            Nodes.Add(node);
            if (properties != null) Properties[node.Id] = properties;
        }
    }
}
