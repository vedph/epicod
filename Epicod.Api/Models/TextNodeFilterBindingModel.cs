using System;
using System.ComponentModel.DataAnnotations;

namespace Epicod.Api.Models
{
    public sealed class TextNodeFilterBindingModel
    {
        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; }

        [Range(1, 100)]
        public int PageSize { get; set; }

        [MaxLength(50)]
        public string CorpusId { get; set; }

        public int ParentId { get; set; }
    }
}
