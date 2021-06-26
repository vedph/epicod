using Epicod.Core;

namespace Epicod.Api.Models
{
    public class TextNodeModel : TextNode
    {
        public TextNodePropertyModel[] Properties { get; set; }
    }

    public class TextNodePropertyModel
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
