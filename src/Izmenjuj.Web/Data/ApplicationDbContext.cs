using Izmenjuj.Web.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Izmenjuj.Web.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionImage> CollectionImages => Set<CollectionImage>();
    public DbSet<CollectibleItem> CollectibleItems => Set<CollectibleItem>();
    public DbSet<ItemImage> ItemImages => Set<ItemImage>();
    public DbSet<UserInventory> UserInventories => Set<UserInventory>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<TradeLine> TradeLines => Set<TradeLine>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<CollectibleItem>()
            .HasIndex(x => new { x.CollectionId, x.InternalCode })
            .IsUnique();

        builder.Entity<UserInventory>()
            .HasIndex(x => new { x.UserId, x.CollectibleItemId, x.State })
            .IsUnique();

        builder.Entity<TradeLine>()
            .HasOne(x => x.Trade)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.TradeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
