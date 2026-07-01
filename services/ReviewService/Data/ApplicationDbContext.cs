using Microsoft.EntityFrameworkCore;
using ReviewService.Models;

namespace ReviewService.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Review> Reviews => Set<Review>();
}