namespace Epicod.Core
{

    /// <summary>
    /// A property attached to a <see cref="TextNodeResult"/>.
    /// </summary>
    public class TextNodeResultProperty
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Converts to string.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return $"{Name}={Value}";
        }
    }
}
