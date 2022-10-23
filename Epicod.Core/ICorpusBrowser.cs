using Fusi.Tools.Data;
using System;
using System.Collections.Generic;

namespace Epicod.Core
{
    /// <summary>
    /// A browser for corpora and their content.
    /// </summary>
    public interface ICorpusBrowser : IDisposable
    {
        /// <summary>
        /// Gets the full list of corpora IDs.
        /// </summary>
        /// <returns>Corpora IDs, alphabetically sorted.</returns>
        IList<string> GetCorpora();

        /// <summary>
        /// Gets the specified page of nodes.
        /// </summary>
        /// <param name="filter">The filter.</param>
        /// <returns>The resulting page.</returns>
        DataPage<TextNodeResult> GetNodes(TextNodeFilter filter);

        /// <summary>
        /// Gets the node with all or a part of its properties.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="propFilters">The property filters; this is null when
        /// all the properties must be retrieved; otherwise, it is a list of
        /// property names, each prefixed with <c>+</c> to include it, or by
        /// <c>-</c> to exclude it. Neither of these prefixes just means to
        /// include the named property. You can use <c>+</c> alone to include
        /// any properties, except the excluded ones if any; or <c>-</c> alone
        /// to exclude any properties, except the included ones if any.</param>
        /// <returns>The result or null if not found.</returns>
        TextNodeResult? GetNode(int id, IList<string>? propFilters = null);
    }
}
