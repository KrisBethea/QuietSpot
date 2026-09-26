namespace QuietSpot.Api.Data;

public static class SeedData
{
    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        if (db.Venues.Any()) return;

        (string Name, string Cat, double Lat, double Lng, string[] Attrs)[] venues =
        [
            ("Spyhouse Coffee - Northeast", "cafe", 45.0036, -93.2532, ["solo_seating", "wifi", "order_at_counter"]),
            ("Spyhouse Coffee - Uptown", "cafe", 44.9550, -93.2980, ["solo_seating", "wifi", "low_music"]),
            ("Walker Library", "library", 44.9484, -93.2983, ["solo_seating", "low_music", "no_talking_zones"]),
            ("Hennepin County Central Library", "library", 44.9802, -93.2716, ["solo_seating", "study_rooms", "no_talking_zones"]),
            ("Dogwood Coffee - East Lake", "cafe", 44.9483, -93.2387, ["wifi", "order_at_counter"]),
            ("YWCA Uptown Fitness", "gym", 44.9489, -93.2926, ["solo_equipment_area"]),
            ("Lunds & Byerlys Uptown", "grocery", 44.9523, -93.2965, ["self_checkout"]),
            ("Sebastian Joe's Ice Cream", "cafe", 44.9617, -93.2944, ["order_at_counter"]),
            ("East Lake Library", "library", 44.9484, -93.2399, ["solo_seating", "low_music"]),
            ("Caribou Coffee - Lyndale", "cafe", 44.9395, -93.2885, ["wifi", "order_at_counter", "drive_thru"]),
        ];

        foreach (var (name, cat, lat, lng, attrs) in venues)
        {
            var venue = new Venue { Name = name, Category = cat, Lat = lat, Lng = lng };
            venue.Attributes.AddRange(attrs.Select(a => new VenueAttribute { VenueId = venue.Id, Attribute = a }));
            db.Venues.Add(venue);
        }

        await db.SaveChangesAsync();
    }
}
