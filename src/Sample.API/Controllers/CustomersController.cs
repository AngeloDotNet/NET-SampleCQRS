using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sample.API.Data;
using Sample.API.Models.Command;

namespace Sample.API.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController(IMediator mediator, ReadDbContext readDb) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateCustomerCommand cmd)
    {
        var id = await mediator.Send(cmd);
        return CreatedAtAction(nameof(GetAsync), new { id }, new { id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id)
    {
        var c = await readDb.Customers.FindAsync(id);

        if (c == null)
        {
            return NotFound();
        }

        return Ok(c);
    }
}