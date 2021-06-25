﻿using System;

namespace Epicod.Scraper
{
    /// <summary>
    /// A property belonging to a <see cref="TextNode"/>. This is any value
    /// encoded as a string, with a name (which is not necessarily unique for
    /// each node) and a node ID.
    /// </summary>
    public class TextNodeProperty
    {
        /// <summary>
        /// Gets or sets identifier of the node this property refers to.
        /// </summary>
        public int NodeId { get; set; }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TextNodeProperty"/> class.
        /// </summary>
        public TextNodeProperty()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TextNodeProperty"/> class.
        /// </summary>
        /// <param name="nodeId">The node identifier.</param>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="ArgumentNullException">name or value</exception>
        public TextNodeProperty(int nodeId, string name, string value)
        {
            NodeId = nodeId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Converts to string.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return $"{Name}={(Value?.Length > 60 ? Value.Substring(0, 60) : Value)} " +
                $"(#{NodeId})";
        }
    }
}
