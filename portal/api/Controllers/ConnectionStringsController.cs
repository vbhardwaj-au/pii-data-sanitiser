using Microsoft.AspNetCore.Mvc;
using MediatR;
using Contracts.DTOs;
using API.Features.ConnectionStrings.Commands.CreateConnectionString;
using API.Features.ConnectionStrings.Commands.UpdateConnectionString;
using API.Features.ConnectionStrings.Commands.DeleteConnectionString;
using API.Features.ConnectionStrings.Queries.GetConnectionString;
using API.Features.ConnectionStrings.Queries.GetConnectionStringsByProduct;
using API.Features.ConnectionStrings.Queries.GetAllConnectionStrings;
using API.Features.ConnectionStrings.Commands.TestConnectionString;
using API.Features.ConnectionStrings.Commands.TestStoredConnectionString;

namespace API.Controllers
{
    /// <summary>
    /// API controller for managing product connection strings
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ConnectionStringsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ConnectionStringsController(IMediator mediator)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        }

        /// <summary>
        /// Get all connection strings, optionally filtered by product ID
        /// </summary>
        /// <param name="productId">Optional product ID filter</param>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductConnectionStringDto>>> GetAll(Guid? productId = null)
        {
            var query = new GetAllConnectionStringsQuery(productId);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>
        /// Get connection strings for a specific product
        /// </summary>
        /// <param name="productId">The product ID</param>
        [HttpGet("product/{productId}")]
        public async Task<ActionResult<IEnumerable<ProductConnectionStringDto>>> GetByProductId(Guid productId)
        {
            var query = new GetConnectionStringsByProductQuery(productId);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>
        /// Get a specific connection string by ID
        /// </summary>
        /// <param name="id">The connection string ID</param>
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductConnectionStringDto>> GetById(Guid id)
        {
            var query = new GetConnectionStringQuery(id);
            var result = await _mediator.Send(query);
            
            if (result == null)
                return NotFound();
                
            return Ok(result);
        }

        /// <summary>
        /// Create a new connection string
        /// </summary>
        /// <param name="command">The connection string creation data</param>
        [HttpPost]
        public async Task<ActionResult<ProductConnectionStringDto>> Create(CreateConnectionStringCommand command)
        {
            try
            {
                var result = await _mediator.Send(command);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        /// <summary>
        /// Update an existing connection string
        /// </summary>
        /// <param name="id">The connection string ID</param>
        /// <param name="command">The updated connection string data</param>
        [HttpPut("{id}")]
        public async Task<ActionResult<ProductConnectionStringDto>> Update(Guid id, UpdateConnectionStringCommand command)
        {
            if (id != command.Id)
                return BadRequest("ID mismatch");

            try
            {
                var result = await _mediator.Send(command);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        /// <summary>
        /// Delete a connection string
        /// </summary>
        /// <param name="id">The connection string ID</param>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(Guid id)
        {
            var command = new DeleteConnectionStringCommand(id);
            var result = await _mediator.Send(command);
            
            if (!result)
                return NotFound();
                
            return NoContent();
        }

        /// <summary>
        /// Test a connection string
        /// </summary>
        /// <param name="command">The connection string test command</param>
        [HttpPost("test")]
        public async Task<ActionResult<ConnectionTestResultDto>> TestConnection(TestConnectionStringCommand command)
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// Test a stored connection string by ID
        /// </summary>
        /// <param name="id">The connection string ID to test</param>
        /// <param name="includeExtendedInfo">Whether to include extended database information</param>
        [HttpPost("{id}/test")]
        public async Task<ActionResult<ConnectionTestResultDto>> TestStoredConnection(Guid id, [FromQuery] bool includeExtendedInfo = false)
        {
            var command = new TestStoredConnectionStringCommand(id, includeExtendedInfo);
            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}