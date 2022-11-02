namespace Epicod.Core
{
    /// <summary>
    /// Constants used for text node properties.
    /// </summary>
    public static class TextNodeProps
    {
        /// <summary>
        /// The inscription's text.
        /// </summary>
        public const string TEXT = "text";

        /// <summary>
        /// The note with metadata about an inscription.
        /// </summary>
        public const string NOTE = "note";

        /// <summary>
        /// The region.
        /// </summary>
        public const string REGION = "region";

        /// <summary>
        /// The site in a region.
        /// </summary>
        public const string SITE = "site";

        /// <summary>
        /// The location in a region.
        /// </summary>
        public const string LOCATION = "location";

        /// <summary>
        /// The inscription type.
        /// </summary>
        public const string TYPE = "type";

        /// <summary>
        /// Forgery rank value: 1=forgery, 2=probable forgery, 3=perhaps forgery.
        /// </summary>
        public const string FORGERY = "forgery";

        /// <summary>
        /// The writing direction / layout.
        /// </summary>
        public const string LAYOUT = "layout";

        /// <summary>
        /// The original date.
        /// </summary>
        public const string DATE_PHI = "date-phi";

        /// <summary>
        /// The date's text in a conventional normal form (Cadmus).
        /// </summary>
        public const string DATE_TXT = "date-txt";

        /// <summary>
        /// The date's (approximate) numeric value.
        /// </summary>
        public const string DATE_VAL = "date-val";

        /// <summary>
        /// A documentary reference.
        /// </summary>
        public const string REFERENCE = "reference";

        /// <summary>
        /// The languages (ISO 639-3, space-delimited, sorted).
        /// </summary>
        public const string LANGUAGES = "languages";

        // data types
        public const string TYPE_INT = "integer";
    }
}
