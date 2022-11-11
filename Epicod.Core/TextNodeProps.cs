using System.Data;

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
        /// The boustrophedon value: 1=boustrophedic, 2=perhaps boustrophedic.
        /// </summary>
        public const string BOUSTR = "boustr";

        /// <summary>
        /// The right-to-left value: 1=RTL, 2=perhaps RTL.
        /// </summary>
        public const string RTL = "rtl";

        /// <summary>
        /// The stoichedon minimum number.
        /// </summary>
        public const string STOICH_MIN = "stoich-min";

        /// <summary>
        /// The stoichedon maximum number.
        /// </summary>
        public const string STOICH_MAX = "stoich-max";

        /// <summary>
        /// The non-stoichedon minimum number.
        /// </summary>
        public const string NON_STOICH_MIN = "non-stoich-min";

        /// <summary>
        /// The non-stoichedon maximum number.
        /// </summary>
        public const string NON_STOICH_MAX = "non-stoich-max";

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
        public const string TYPE_POINT = "point";
    }
}
