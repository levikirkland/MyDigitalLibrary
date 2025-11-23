using Microsoft.AspNetCore.Mvc;
using MyDigitalLibrary.Repositories;
using MyDigitalLibrary.Services;
using MyDigitalLibrary.Entities;

namespace MyDigitalLibrary.Controllers;

[ApiController]
[Route("Collections")]
public class CollectionsController : ControllerBase
{
    private readonly ICollectionRepository _repo;

    public CollectionsController(ICollectionRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("ListForUser")]
    public async Task<IActionResult> ListForUser()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return Unauthorized();
        var cols = await _repo.GetCollectionsByUserIdAsync(userId);
        var res = cols.Select(c => new { id = c.Id, name = c.Name, description = c.Description });
        return Ok(res);
    }

    public class CreateDto { public string? Name { get; set; } public string? Description { get; set; } }

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] CreateDto dto)
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto?.Name)) return BadRequest("Name is required");
        var c = new CollectionEntity { UserId = userId, Name = dto.Name.Trim(), Description = dto.Description };
        var added = await _repo.AddAsync(c);
        return CreatedAtAction(nameof(Get), new { id = added.Id }, new { id = added.Id, name = added.Name });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var c = await _repo.GetCollectionByIdAsync(id);
        if (c == null) return NotFound();
        return Ok(c);
    }

    public class AddBookDto { public int CollectionId { get; set; } public int BookId { get; set; } }

    [HttpPost("AddBook")]
    public async Task<IActionResult> AddBook([FromBody] AddBookDto dto)
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return Unauthorized();
        var c = await _repo.GetCollectionByIdAsync(dto.CollectionId);
        if (c == null || c.UserId != userId) return Forbid();
        await _repo.AddBookAsync(new BookCollectionEntity { CollectionId = dto.CollectionId, BookId = dto.BookId });
        return Ok();
    }

    [HttpPost("RemoveBook")]
    public async Task<IActionResult> RemoveBook([FromBody] AddBookDto dto)
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return Unauthorized();
        var c = await _repo.GetCollectionByIdAsync(dto.CollectionId);
        if (c == null || c.UserId != userId) return Forbid();
        await _repo.RemoveBookAsync(dto.BookId, dto.CollectionId);
        return Ok();
    }
}
