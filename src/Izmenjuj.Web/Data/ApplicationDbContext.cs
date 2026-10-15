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
    public DbSet<TradeMessage> TradeMessages => Set<TradeMessage>();
    public DbSet<TradeEvent> TradeEvents => Set<TradeEvent>();
    public DbSet<TradeRating> TradeRatings => Set<TradeRating>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Collection>()
            .HasIndex(x => new { x.IsPublished, x.UpdatedUtc });

        builder.Entity<Collection>()
            .HasIndex(x => x.CreatedByUserId);

        builder.Entity<CollectibleItem>()
            .HasIndex(x => new { x.CollectionId, x.InternalCode })
            .IsUnique();

        builder.Entity<UserInventory>()
            .HasIndex(x => new { x.UserId, x.CollectibleItemId, x.State })
            .IsUnique();

        builder.Entity<Trade>()
            .HasIndex(x => new { x.ProposerUserId, x.Status, x.CreatedUtc });

        builder.Entity<Trade>()
            .HasIndex(x => new { x.RecipientUserId, x.Status, x.CreatedUtc });

        builder.Entity<TradeLine>()
            .HasOne(x => x.Trade)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.TradeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TradeMessage>()
            .HasOne(x => x.Trade)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.TradeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TradeMessage>()
            .HasIndex(x => new { x.TradeId, x.SentUtc });

        builder.Entity<TradeEvent>()
            .HasOne(x => x.Trade)
            .WithMany(x => x.Events)
            .HasForeignKey(x => x.TradeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TradeEvent>()
            .HasIndex(x => new { x.TradeId, x.CreatedUtc });

        builder.Entity<TradeRating>()
            .HasOne(x => x.Trade)
            .WithMany(x => x.Ratings)
            .HasForeignKey(x => x.TradeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TradeRating>()
            .HasIndex(x => new { x.TradeId, x.FromUserId })
            .IsUnique();
    }
}
