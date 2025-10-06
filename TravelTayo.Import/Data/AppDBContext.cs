using HotelbedsAPI.Models;
using Microsoft.EntityFrameworkCore;
using TravelTayo.Import.Models;

namespace TravelTayo.Import.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<State> States => Set<State>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryGroup> CategoryGroups => Set<CategoryGroup>();
    public DbSet<AccommodationType> AccommodationTypes => Set<AccommodationType>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<Segment> Segments => Set<Segment>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Rooms> Rooms => Set<Rooms>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<Image> Images => Set<Image>();
    public DbSet<HotelPhone> HotelPhones => Set<HotelPhone>();
    public DbSet<HotelWildcard> HotelWildcards => Set<HotelWildcard>();

    public DbSet<RoomFacility> RoomFacility => Set<RoomFacility>();

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

            // Relationship with HotelPhone
            b.HasMany(h => h.Phones)         // navigation property in Hotel
             .WithOne(p => p.Hotel)          // navigation property in HotelPhone
             .HasForeignKey(p => p.HotelId)  // foreign key in HotelPhone
             .OnDelete(DeleteBehavior.Cascade); // optional: delete phones if hotel deleted
        });

        // Simple keys for the rest
        modelBuilder.Entity<Country>().HasKey(c => c.Id);
        modelBuilder.Entity<State>().HasKey(s => s.Id);
        modelBuilder.Entity<Zone>().HasKey(z => z.Id);
        modelBuilder.Entity<Category>().HasKey(c => c.Id);
        modelBuilder.Entity<CategoryGroup>().HasKey(cg => cg.Id);
        modelBuilder.Entity<AccommodationType>().HasKey(a => a.Id);
        modelBuilder.Entity<Address>().HasKey(a => a.Id);
        modelBuilder.Entity<Board>().HasKey(b => b.Id);
        modelBuilder.Entity<Rooms>().HasKey(r => r.Id);
        modelBuilder.Entity<Facility>().HasKey(f => f.Id);
        modelBuilder.Entity<Terminal>().HasKey(t => t.Id);
        modelBuilder.Entity<Image>().HasKey(i => i.Id);
        modelBuilder.Entity<HotelPhone>().HasKey(p => p.Id);
        modelBuilder.Entity<HotelWildcard>().HasKey(w => w.Id);
        modelBuilder.Entity<RoomFacility>().HasKey(rf => rf.Id);

        modelBuilder.Entity<HotelPhone>(b =>
        {
            b.HasKey(p => p.Id);             // Define the primary key
            b.Property(p => p.Id)             // Configure the property
             .ValueGeneratedOnAdd();          // Auto-increment
        });
    }
}
