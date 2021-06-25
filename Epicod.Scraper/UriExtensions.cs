using System;

namespace Epicod.Scraper
{
    /// <summary>
    /// Extension methods for Uri.
    /// </summary>
    static public class UriExtensions
    {
        public static string ToRelative(this Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            return uri.IsAbsoluteUri ? uri.PathAndQuery : uri.OriginalString;
        }

        public static string ToAbsolute(this Uri uri, string baseUrl)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));
            if (baseUrl == null)
                throw new ArgumentNullException(nameof(baseUrl));

            Uri baseUri = new Uri(baseUrl);

            return uri.ToAbsolute(baseUri);
        }

        public static string ToAbsolute(this Uri uri, Uri baseUri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            if (baseUri == null)
                throw new ArgumentNullException(nameof(baseUri));

            string relative = uri.ToRelative();

            if (Uri.TryCreate(baseUri, relative, out var absolute))
                return absolute.ToString();

            return uri.IsAbsoluteUri ? uri.ToString() : null;
        }
    }
}
