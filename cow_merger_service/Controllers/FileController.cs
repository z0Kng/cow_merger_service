using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using cow_merger_service.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;


namespace cow_merger_service.Controllers
{
    [ApiController]
    [Route("v1/[controller]/[action]")]
    public class FileController : Controller
    {
        private SessionManager _sessionManager;
        public FileController(SessionManager sessionManager, ILogger<FileController> logger)
        {
            this._sessionManager = sessionManager;
        }

        [HttpPost]
        public ActionResult<Guid> Create([Required, FromForm] int version, [Required, FromForm] int bitfieldSize, [Required, FromForm] string imageName)
        {
            try
            {
                return Ok(_sessionManager.Create(imageName, version, bitfieldSize).ToString());
            }
            catch (ImageNotFound)
            {
                return NotFound("Image not found");
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost]
        [Consumes("application/octet-stream")]
        public async Task<ActionResult> Update([Required, FromQuery] Guid uuid, [Required, FromQuery] int clusterindex)//, [Required, FromBody] byte[] data)
        {

       
            byte[] data;
            using (var ms = new MemoryStream(2048))
            {
                 await Request.Body.CopyToAsync(ms);
                 data = ms.ToArray();
            }
            try
            {
                if (_sessionManager.Update(uuid, clusterindex, data.AsSpan()))
                {
                    return Ok();
                }
                else
                {
                    return StatusCode(StatusCodes.Status500InternalServerError);
                }
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Session not found");
            }
        }

        [HttpPost]
        public ActionResult Merge([Required, FromForm] Guid uuid, [Required, FromForm] long originalFileSize,  [Required, FromForm] long newFileSize)
        {
            try
            {
                return Ok(_sessionManager.StartMerge(uuid, originalFileSize, newFileSize));
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Session not found");
            }
            catch (InvalidOperationException)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet]
        [Produces("application/json")]
        public ActionResult<List<BlockStatistics>> GetTopModifiedBlocks([Required] Guid uuid, [Required] int amount)
        {
            try {
                return Ok(_sessionManager.GetTopModifiedBlocks(uuid, amount));
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Session not found");
            }
        }

        [HttpGet]
        public ActionResult<SessionStatus> Status([Required] Guid uuid)
        {
            try
            {
                return Ok(_sessionManager.Status(uuid));
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Session not found");
            }
        }
    }
}