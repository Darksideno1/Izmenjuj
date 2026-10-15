using Izmenjuj.Web.Data;
using Izmenjuj.Web.Domain;
using Izmenjuj.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izmenjuj.Web.Controllers;

[Authorize]
public sealed class InventoryController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Collection(Guid id, CancellationToken cancellationToken)
    {
        var model = await BuildModel(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Collection(CollectionInventoryViewModel model, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var collection = await db.Collections
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == model.CollectionId, cancellationToken);

        if (collection is null || (!collection.IsPublished && collection.CreatedByUserId != userId))
        {
            return NotFound();
        }

        var validItemIds = collection.Items.Select(x => x.Id).ToHashSet();
        var submittedRows = model.Rows
            .Where(x => validItemIds.Contains(x.ItemId))
            .GroupBy(x => x.ItemId)
            .Select(x => x.First())
            .ToList();

        foreach (var row in submittedRows)
        {
            if (row.HaveQuantity < 0 || row.HaveQuantity > 999 || row.ForTradeQuantity < 0 || row.ForTradeQuantity > 999)
            {
                ModelState.AddModelError(string.Empty, "Količina mora biti med 0 in 999.");
            }

            if (row.ForTradeQuantity > row.HaveQuantity)
            {
                ModelState.AddModelError(string.Empty, "Količina za menjavo ne sme biti večja od količine, ki jo imaš.");
            }

            if (row.Wanted && row.HaveQuantity > 0)
            {
                ModelState.AddModelError(string.Empty, "Predmet ne more biti hkrati označen kot 'Iščem' in 'Imam'.");
            }
        }

        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildModel(model.CollectionId, cancellationToken);
            if (invalidModel is null)
            {
                return NotFound();
            }

            foreach (var submitted in submittedRows)
            {
                var target = invalidModel.Rows.FirstOrDefault(x => x.ItemId == submitted.ItemId);
                if (target is not null)
                {
                    target.HaveQuantity = submitted.HaveQuantity;
                    target.Wanted = submitted.Wanted;
                    target.ForTradeQuantity = submitted.ForTradeQuantity;
                }
            }

            return View(invalidModel);
        }

        var existing = await db.UserInventories
            .Where(x => x.UserId == userId && validItemIds.Contains(x.CollectibleItemId))
            .ToListAsync(cancellationToken);

        foreach (var row in submittedRows)
        {
            SyncState(existing, userId, row.ItemId, InventoryState.Have, row.HaveQuantity);
            SyncState(existing, userId, row.ItemId, InventoryState.Wanted, row.Wanted ? 1 : 0);
            SyncState(existing, userId, row.ItemId, InventoryState.ForTrade, row.ForTradeQuantity);
        }

        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Tvoja evidenca je shranjena.";
        return RedirectToAction(nameof(Collection), new { id = model.CollectionId });
    }

    private void SyncState(
        List<UserInventory> existing,
        string userId,
        Guid itemId,
        InventoryState state,
        int quantity)
    {
        var entry = existing.FirstOrDefault(x => x.CollectibleItemId == itemId && x.State == state);
        if (quantity <= 0)
        {
            if (entry is not null)
            {
                db.UserInventories.Remove(entry);
                existing.Remove(entry);
            }
            return;
        }

        if (entry is null)
        {
            entry = new UserInventory
            {
                UserId = userId,
                CollectibleItemId = itemId,
                State = state,
                Quantity = quantity
            };
            db.UserInventories.Add(entry);
            existing.Add(entry);
        }
        else
        {
            entry.Quantity = quantity;
        }
    }

    private async Task<CollectionInventoryViewModel?> BuildModel(Guid collectionId, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return null;
        }

        var collection = await db.Collections
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == collectionId, cancellationToken);

        if (collection is null || (!collection.IsPublished && collection.CreatedByUserId != userId))
        {
            return null;
        }

        var itemIds = collection.Items.Select(x => x.Id).ToList();
        var inventory = await db.UserInventories
            .AsNoTracking()
            .Where(x => x.UserId == userId && itemIds.Contains(x.CollectibleItemId))
            .ToListAsync(cancellationToken);

        var rows = collection.Items
            .OrderBy(x => SortKey(x.OfficialCode, x.InternalCode))
            .ThenBy(x => x.Name)
            .Select(item => new InventoryRowViewModel
            {
                ItemId = item.Id,
                Name = item.Name,
                OfficialCode = item.OfficialCode,
                InternalCode = item.InternalCode,
                Variant = item.Variant,
                PrimaryImagePath = item.PrimaryImagePath,
                HaveQuantity = inventory.FirstOrDefault(x => x.CollectibleItemId == item.Id && x.State == InventoryState.Have)?.Quantity ?? 0,
                Wanted = inventory.Any(x => x.CollectibleItemId == item.Id && x.State == InventoryState.Wanted),
                ForTradeQuantity = inventory.FirstOrDefault(x => x.CollectibleItemId == item.Id && x.State == InventoryState.ForTrade)?.Quantity ?? 0
            })
            .ToList();

        return new CollectionInventoryViewModel
        {
            CollectionId = collection.Id,
            Collection = collection,
            Rows = rows,
            TotalItems = rows.Count,
            OwnedItems = rows.Count(x => x.HaveQuantity > 0),
            WantedItems = rows.Count(x => x.Wanted),
            ForTradeItems = rows.Sum(x => x.ForTradeQuantity)
        };
    }

    private static string SortKey(string? officialCode, string internalCode) =>
        string.IsNullOrWhiteSpace(officialCode) ? internalCode : officialCode;
}
