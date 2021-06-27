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
        public DataPage<TextNodeResult> GetNodes(
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
        public ActionResult<TextNodeResult> GetNode(
            [FromRoute] int id,
            [FromQuery] string propFilters)
        {
            TextNodeResult node = _browser.GetNode(id,
                propFilters.Split(new[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries));
            if (node == null) return NotFound();
            return Ok(node);
        }
    }
}
