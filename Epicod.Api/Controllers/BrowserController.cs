using Epicod.Api.Models;
using Epicod.Core;
using Fusi.Tools.Data;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace Epicod.Api.Controllers
{
    [ApiController]
    public class BrowserController : ControllerBase
    {
        private readonly ICorpusBrowser _browser;

        public BrowserController(ICorpusBrowser browser)
        {
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        }

        [HttpGet("api/corpora")]
        [ProducesResponseType(200)]
        public string[] GetCorpora()
        {
            return _browser.GetCorpora().ToArray();
        }

        [HttpGet("api/nodes")]
        [ProducesResponseType(200)]
        public DataPage<TextNode> GetNodes(
            [FromQuery] TextNodeFilterBindingModel model)
        {
            TextNodeFilter filter = new TextNodeFilter
            {
                PageNumber = model.PageNumber,
                PageSize = model.PageSize,
                CorpusId = model.CorpusId,
                ParentId = model.ParentId
            };
            return _browser.GetNodes(filter);
        }

        [HttpGet("api/nodes/{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public ActionResult<TextNodeModel> GetNode(
            [FromRoute] int id,
            [FromQuery] string propFilters)
        {
            var t = _browser.GetNode(id,
                propFilters.Split(new[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries));
            if (t == null) return NotFound();

            TextNodeModel model = new TextNodeModel
            {
                Id = t.Item1.Id,
                ParentId = t.Item1.ParentId,
                Corpus = t.Item1.Corpus,
                Y = t.Item1.Y,
                X = t.Item1.X,
                Name = t.Item1.Name,
                Uri = t.Item1.Uri,
                Properties = t.Item2.Select(p => new TextNodePropertyModel
                {
                    Name = p.Name,
                    Value = p.Value
                }).ToArray()
            };
            return Ok(model);
        }
    }
}
