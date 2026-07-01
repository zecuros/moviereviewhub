using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReviewService.Data;
using ReviewService.Models;

namespace ReviewService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ReviewController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("movie/{movieId}")]
    public async Task<ActionResult<IEnumerable<Review>>> GetByMovie(int movieId)
    {
        var reviews = await _context.Reviews
            .Where(r => r.MovieId == movieId)
            .ToListAsync();

        return Ok(reviews);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<Review>>> GetByUser(int userId)
    {
        var reviews = await _context.Reviews
            .Where(r => r.UserId == userId)
            .ToListAsync();

        return Ok(reviews);
    }

    [HttpGet("average/{movieId}")]
    public async Task<ActionResult<double>> GetAverageRating(int movieId)
    {
        var reviews = await _context.Reviews
            .Where(r => r.MovieId == movieId)
            .ToListAsync();

        if (!reviews.Any())
            return Ok(0);

        return Ok(reviews.Average(r => r.Rating));
    }

    [HttpPost]
    public async Task<ActionResult<Review>> Create(Review review)
    {
        if (review.Rating < 1 || review.Rating > 5)
            return BadRequest("Rating must be between 1 and 5.");

        review.CreatedAt = DateTime.UtcNow;

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByMovie), new { movieId = review.MovieId }, review);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var review = await _context.Reviews.FindAsync(id);

        if (review == null)
            return NotFound();

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}