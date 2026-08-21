using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reactive.Threading.Tasks;
using WatchlistService.Data;
using WatchlistService.Models;
using WatchlistService.Services;

namespace WatchlistService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WatchlistController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly MovieReactiveClient _movieClient;

    public WatchlistController(
        ApplicationDbContext context,
        MovieReactiveClient movieClient)
    {
        _context = context;
        _movieClient = movieClient;
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<WatchlistItem>>> GetByUser(int userId)
    {
        var items = await _context.WatchlistItems
            .Where(w => w.UserId == userId)
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<WatchlistItem>> Add(WatchlistItem item)
    {
        var movieExists = await _movieClient
            .MovieExists(item.MovieId)
            .ToTask();

        if (!movieExists)
            return BadRequest("Movie does not exist.");

        var alreadyExists = await _context.WatchlistItems.AnyAsync(w =>
            w.UserId == item.UserId &&
            w.MovieId == item.MovieId);

        if (alreadyExists)
            return BadRequest("Movie already exists in user's watchlist.");

        item.AddedAt = DateTime.UtcNow;

        _context.WatchlistItems.Add(item);
        await _context.SaveChangesAsync();

        return Ok(item);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(int id)
    {
        var item = await _context.WatchlistItems.FindAsync(id);

        if (item == null)
            return NotFound();

        _context.WatchlistItems.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}