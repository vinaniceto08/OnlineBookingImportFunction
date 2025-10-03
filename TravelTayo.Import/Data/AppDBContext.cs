using Microsoft.EntityFrameworkCore;
using TravelTayo.Import.Models;

namespace TravelTayo.Import.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<State> States => Set<State>();
    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryGroup> CategoryGroups => Set<CategoryGroup>();
    public DbSet<Chain> Chains => Set<Chain>();
    public DbSet<AccommodationType> AccommodationTypes => Set<AccommodationType>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<Segment> Segments => Set<Segment>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<Image> Images => Set<Image>();
    public DbSet<HotelPhone> HotelPhones => Set<HotelPhone>();
    public DbSet<HotelWildcard> HotelWildcards => Set<HotelWildcard>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Hotel>(b =>
        {
            b.HasKey(h => h.Id);
            b.Property(h => h.Name).HasMaxLength(500);
            b.Property(h => h.Web).HasMaxLength(500);
            b.Property(h => h.Email).HasMaxLength(250);
            b.Property(h => h.Description).HasMaxLength(2000);
            b.Property(h => h.LastUpdate).HasDefaultValueSql("GETUTCDATE()");
            b.HasIndex(h => h.GiataCode);
        });

        // Simple keys for the rest
        modelBuilder.Entity<Country>().HasKey(c => c.Id);
        modelBuilder.Entity<State>().HasKey(s => s.Id);
        modelBuilder.Entity<Destination>().HasKey(d => d.Id);
        modelBuilder.Entity<Zone>().HasKey(z => z.Id);
        modelBuilder.Entity<Category>().HasKey(c => c.Id);
        modelBuilder.Entity<CategoryGroup>().HasKey(cg => cg.Id);
        modelBuilder.Entity<Chain>().HasKey(c => c.Id);
        modelBuilder.Entity<AccommodationType>().HasKey(a => a.Id);
        modelBuilder.Entity<Address>().HasKey(a => a.Id);
        modelBuilder.Entity<Board>().HasKey(b => b.Id);
        modelBuilder.Entity<Room>().HasKey(r => r.Id);
        modelBuilder.Entity<Facility>().HasKey(f => f.Id);
        modelBuilder.Entity<Terminal>().HasKey(t => t.Id);
        modelBuilder.Entity<Image>().HasKey(i => i.Id);
        modelBuilder.Entity<HotelPhone>().HasKey(p => p.Id);
        modelBuilder.Entity<HotelWildcard>().HasKey(w => w.Id);
    }
}
