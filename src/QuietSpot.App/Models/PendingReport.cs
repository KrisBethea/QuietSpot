using SQLite;

namespace QuietSpot.App.Models;

public class PendingReport
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public Guid VenueId { get; set; }
    public double Level { get; set; }
    public string Source { get; set; } = "in_app";
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
