using Izmenjuj.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Izmenjuj.Web.Data;

public static class PreviewDataSeeder
{
    public const string DemoEmail = "demo@izmenjuj.local";
    public const string DemoPassword = "Demo1234";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        var demo = await userManager.FindByEmailAsync(DemoEmail);
        if (demo is null)
        {
            demo = new ApplicationUser
            {
                UserName = DemoEmail,
                Email = DemoEmail,
                EmailConfirmed = true,
                DisplayName = "Demo uporabnik",
                City = "Ljubljana",
                TradesByMail = true,
                TradesInPerson = true
            };
            var created = await userManager.CreateAsync(demo, DemoPassword);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", created.Errors.Select(x => x.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(demo, "Admin"))
        {
            await userManager.AddToRoleAsync(demo, "Admin");
        }

        const string partnerEmail = "miha@izmenjuj.local";
        var partner = await userManager.FindByEmailAsync(partnerEmail);
        if (partner is null)
        {
            partner = new ApplicationUser
            {
                UserName = partnerEmail,
                Email = partnerEmail,
                EmailConfirmed = true,
                DisplayName = "Miha K.",
                City = "Ljubljana",
                TradesByMail = true,
                TradesInPerson = true
            };
            var created = await userManager.CreateAsync(partner, DemoPassword);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", created.Errors.Select(x => x.Description)));
            }
        }

        if (await db.Collections.AnyAsync(cancellationToken))
        {
            return;
        }

        var fifa = new Collection
        {
            Title = "Panini FIFA 365",
            Subtitle = "Sezona 2026",
            Description = "Primer številčenega albuma. Slike predmetov so namenoma izklopljene, zato je seznam hiter in kompakten.",
            Year = 2026,
            Manufacturer = "Panini",
            Country = "International",
            Type = CollectionType.Stickers,
            ShowItemImages = false,
            IsPublished = true,
            CreatedByUserId = demo.Id
        };
        var fifaItems = new[]
        {
            NewItem(fifa, "001", "Lionel Messi", "Base"),
            NewItem(fifa, "002", "Jude Bellingham", "Star"),
            NewItem(fifa, "003", "Kylian Mbappé", "Base"),
            NewItem(fifa, "004", "Lamine Yamal", "Rising Star"),
            NewItem(fifa, "014", "Vinícius Jr.", "Base"),
            NewItem(fifa, "051", "Erling Haaland", "Base")
        };
        fifa.Items.AddRange(fifaItems);

        var kinder = new Collection
        {
            Title = "Kinder Surprise – Fantasy",
            Subtitle = "Figurice in različice",
            Description = "Primer zbirke, kjer je slikovni način smiseln. V produkciji bi se prikazale naložene fotografije predmetov.",
            Year = 2026,
            Manufacturer = "Ferrero",
            Type = CollectionType.Figurines,
            ShowItemImages = true,
            IsPublished = true,
            CreatedByUserId = demo.Id
        };
        kinder.Items.AddRange(
            NewItem(kinder, null, "Modri zmaj", "Standard", "KIN-001"),
            NewItem(kinder, null, "Rdeči vitez", "Metallic", "KIN-002"),
            NewItem(kinder, null, "Čarovnica", "Standard", "KIN-003"),
            NewItem(kinder, null, "Gozdni škrat", "Green cape", "KIN-004"));

        var cards = new Collection
        {
            Title = "Harry Potter Trading Cards",
            Subtitle = "Wizarding World",
            Description = "Primer kart, pri katerih je mogoče slikovni prikaz vključiti ali izključiti neodvisno od tipa zbirke.",
            Year = 2025,
            Manufacturer = "Demo",
            Type = CollectionType.Cards,
            ShowItemImages = true,
            IsPublished = true,
            CreatedByUserId = demo.Id
        };
        cards.Items.AddRange(
            NewItem(cards, "HP-001", "Harry Potter", "Base"),
            NewItem(cards, "HP-002", "Hermione Granger", "Foil"),
            NewItem(cards, "HP-003", "Ron Weasley", "Base"));

        db.Collections.AddRange(fifa, kinder, cards);

        // Demo user: owns / can trade 003 and 014, wants 002 and 051.
        db.UserInventories.AddRange(
            Inventory(demo.Id, fifaItems[2].Id, InventoryState.Have, 2),
            Inventory(demo.Id, fifaItems[2].Id, InventoryState.ForTrade, 1),
            Inventory(demo.Id, fifaItems[4].Id, InventoryState.Have, 2),
            Inventory(demo.Id, fifaItems[4].Id, InventoryState.ForTrade, 1),
            Inventory(demo.Id, fifaItems[1].Id, InventoryState.Wanted, 1),
            Inventory(demo.Id, fifaItems[5].Id, InventoryState.Wanted, 1));

        // Partner has exactly the complementary inventory so Matching shows a useful result.
        db.UserInventories.AddRange(
            Inventory(partner.Id, fifaItems[1].Id, InventoryState.Have, 2),
            Inventory(partner.Id, fifaItems[1].Id, InventoryState.ForTrade, 1),
            Inventory(partner.Id, fifaItems[5].Id, InventoryState.Have, 2),
            Inventory(partner.Id, fifaItems[5].Id, InventoryState.ForTrade, 1),
            Inventory(partner.Id, fifaItems[2].Id, InventoryState.Wanted, 1),
            Inventory(partner.Id, fifaItems[4].Id, InventoryState.Wanted, 1));

        var trade = new Trade
        {
            ProposerUserId = partner.Id,
            RecipientUserId = demo.Id,
            Status = TradeStatus.Proposed,
            CreatedUtc = DateTime.UtcNow.AddHours(-2)
        };
        trade.Lines.AddRange(
            new TradeLine { FromUserId = demo.Id, CollectibleItemId = fifaItems[2].Id, Quantity = 1 },
            new TradeLine { FromUserId = demo.Id, CollectibleItemId = fifaItems[4].Id, Quantity = 1 },
            new TradeLine { FromUserId = partner.Id, CollectibleItemId = fifaItems[1].Id, Quantity = 1 },
            new TradeLine { FromUserId = partner.Id, CollectibleItemId = fifaItems[5].Id, Quantity = 1 });
        trade.Events.Add(new TradeEvent
        {
            ActorUserId = partner.Id,
            Type = TradeEventType.Proposed,
            CreatedUtc = trade.CreatedUtc
        });
        trade.Messages.Add(new TradeMessage
        {
            SenderUserId = partner.Id,
            Body = "Pozdravljen! Bi ti ustrezala ta menjava?",
            SentUtc = trade.CreatedUtc.AddMinutes(3)
        });
        trade.Messages.Add(new TradeMessage
        {
            SenderUserId = demo.Id,
            Body = "Videti je dobro. Pregledam še predmete.",
            SentUtc = trade.CreatedUtc.AddMinutes(12)
        });
        db.Trades.Add(trade);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static CollectibleItem NewItem(Collection collection, string? officialCode, string name, string? variant, string? internalCode = null)
        => new()
        {
            Collection = collection,
            CollectionId = collection.Id,
            OfficialCode = officialCode,
            InternalCode = internalCode ?? $"{collection.Id:N}-{officialCode ?? name}"[..Math.Min(80, $"{collection.Id:N}-{officialCode ?? name}".Length)],
            Name = name,
            Variant = variant
        };

    private static UserInventory Inventory(string userId, Guid itemId, InventoryState state, int quantity)
        => new()
        {
            UserId = userId,
            CollectibleItemId = itemId,
            State = state,
            Quantity = quantity
        };
}
