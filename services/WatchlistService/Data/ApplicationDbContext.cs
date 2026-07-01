using Microsoft.EntityFrameworkCore;
using WatchlistService.Models;

namespace WatchlistService.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
}