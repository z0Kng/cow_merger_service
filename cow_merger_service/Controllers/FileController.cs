using System;
using System.Collections.Generic;
using cow_merger_service.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;


namespace cow_merger_service.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class FileController : Controller
    {
        private SessionManager _sessionManager;
        public FileController(SessionManager sessionManager, ILogger<FileController> logger)
        {
            this._sessionManager = sessionManager;
        }

        [HttpPost]
        public ActionResult<Guid> Create([FromForm] int version, [FromForm] int bitfieldSize, [FromForm] string imageName)
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
        public ActionResult Update(Guid guid, int blockNumber, [FromBody] byte[] data)
        {

            try
            {
                if (_sessionManager.Update(guid, blockNumber, data.AsSpan()))
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
        public ActionResult StartMerge([FromForm] Guid guid, [FromForm] long fileSize)
        {
            try
            {
                return Ok(_sessionManager.StartMerge(guid, fileSize));
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
        public ActionResult<SessionStatus> Status(Guid guid)
        {
            try
            {
                return Ok(_sessionManager.Status(guid));
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Session not found");
            }
        }
    }
}