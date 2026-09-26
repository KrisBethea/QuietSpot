using Microsoft.EntityFrameworkCore;

namespace QuietSpot.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Baseline> Baselines => Set<Baseline>();
    public DbSet<VenueAttribute> VenueAttributes => Set<VenueAttribute>();
    public DbSet<Favorite> Favorites => Set<Favorite>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Baseline>().HasKey(x => new { x.VenueId, x.HourOfWeek });
        b.Entity<VenueAttribute>().HasKey(x => new { x.VenueId, x.Attribute });
        b.Entity<Favorite>().HasKey(x => new { x.DeviceId, x.VenueId });

        b.Entity<Report>().HasIndex(x => new { x.VenueId, x.CreatedAtUtc });
        b.Entity<Report>().HasIndex(x => new { x.DeviceId, x.VenueId, x.CreatedAtUtc });
        b.Entity<Venue>().HasIndex(x => new { x.Lat, x.Lng }); // bounding-box prefilter
    }
}
