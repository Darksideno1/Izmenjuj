using Izmenjuj.Web.Data;
using Izmenjuj.Web.Domain;
using Izmenjuj.Web.Services;
using Izmenjuj.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izmenjuj.Web.Controllers;

public sealed class ItemsController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IImageStorage imageStorage) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        var item = await db.CollectibleItems
            .AsNoTracking()
            .Include(x => x.Collection)
            .Include(x => x.Images.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null || (!item.Collection.IsPublished && item.Collection.CreatedByUserId != userId))
        {
            return NotFound();
        }

        ViewData["CanEdit"] = userId != null && item.Collection.CreatedByUserId == userId;
        return View(item);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Create(Guid collectionId, CancellationToken cancellationToken)
    {
        var collection = await FindOwnedCollection(collectionId, cancellationToken);
        if (collection is null)
        {
            return NotFound();
        }

        return View("Edit", new ItemEditViewModel
        {
            CollectionId = collection.Id,
            CollectionTitle = collection.Title
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ItemEditViewModel model, CancellationToken cancellationToken)
    {
        var collection = await FindOwnedCollection(model.CollectionId, cancellationToken);
        if (collection is null)
        {
            return NotFound();
        }

        model.CollectionTitle = collection.Title;
        if (!ModelState.IsValid)
        {
            return View("Edit", model);
        }

        var item = new CollectibleItem
        {
            CollectionId = collection.Id,
            Name = model.Name.Trim(),
            OfficialCode = Clean(model.OfficialCode),
            InternalCode = string.IsNullOrWhiteSpace(model.InternalCode)
                ? NewInternalCode()
                : model.InternalCode.Trim(),
            Description = Clean(model.Description),
            Variant = Clean(model.Variant)
        };

        if (await InternalCodeExists(collection.Id, item.InternalCode, null, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.InternalCode), "Ta interna oznaka v zbirki že obstaja.");
            return View("Edit", model);
        }

        try
        {
            if (model.PrimaryImage is not null)
            {
                item.PrimaryImagePath = await imageStorage.SaveAsync(model.PrimaryImage, "item-primary", cancellationToken);
            }

            var sortOrder = 0;
            foreach (var image in model.GalleryImages.Where(x => x.Length > 0))
            {
                item.Images.Add(new ItemImage
                {
                    ImagePath = await imageStorage.SaveAsync(image, "item-gallery", cancellationToken),
                    SortOrder = sortOrder++
                });
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.CurrentPrimaryImagePath = item.PrimaryImagePath;
            model.CurrentImages = item.Images;
            return View("Edit", model);
        }

        db.CollectibleItems.Add(item);
        collection.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Predmet je dodan v zbirko.";
        return RedirectToAction("Details", "Collections", new { id = collection.Id });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var item = await FindOwnedItem(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        return View(new ItemEditViewModel
        {
            Id = item.Id,
            CollectionId = item.CollectionId,
            CollectionTitle = item.Collection.Title,
            Name = item.Name,
            OfficialCode = item.OfficialCode,
            InternalCode = item.InternalCode,
            Description = item.Description,
            Variant = item.Variant,
            CurrentPrimaryImagePath = item.PrimaryImagePath,
            CurrentImages = item.Images.OrderBy(x => x.SortOrder).ToList()
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ItemEditViewModel model, CancellationToken cancellationToken)
    {
        var item = await FindOwnedItem(model.Id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        model.CollectionId = item.CollectionId;
        model.CollectionTitle = item.Collection.Title;
        if (!ModelState.IsValid)
        {
            FillCurrentMedia(model, item);
            return View(model);
        }

        var proposedInternalCode = string.IsNullOrWhiteSpace(model.InternalCode)
            ? item.InternalCode
            : model.InternalCode.Trim();
        if (await InternalCodeExists(item.CollectionId, proposedInternalCode, item.Id, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.InternalCode), "Ta interna oznaka v zbirki že obstaja.");
            FillCurrentMedia(model, item);
            return View(model);
        }

        item.Name = model.Name.Trim();
        item.OfficialCode = Clean(model.OfficialCode);
        item.InternalCode = proposedInternalCode;
        item.Description = Clean(model.Description);
        item.Variant = Clean(model.Variant);
        item.Collection.UpdatedUtc = DateTime.UtcNow;

        try
        {
            if (model.PrimaryImage is not null)
            {
                var oldImage = item.PrimaryImagePath;
                item.PrimaryImagePath = await imageStorage.SaveAsync(model.PrimaryImage, "item-primary", cancellationToken);
                await imageStorage.DeleteAsync(oldImage, cancellationToken);
            }

            var sortOrder = item.Images.Count == 0 ? 0 : item.Images.Max(x => x.SortOrder) + 1;
            foreach (var image in model.GalleryImages.Where(x => x.Length > 0))
            {
                item.Images.Add(new ItemImage
                {
                    ImagePath = await imageStorage.SaveAsync(image, "item-gallery", cancellationToken),
                    SortOrder = sortOrder++
                });
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            FillCurrentMedia(model, item);
            return View(model);
        }

        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Predmet je shranjen.";
        return RedirectToAction(nameof(Details), new { id = item.Id });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        var item = await FindOwnedItem(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        var image = item.Images.FirstOrDefault(x => x.Id == imageId);
        if (image is null)
        {
            return NotFound();
        }

        await imageStorage.DeleteAsync(image.ImagePath, cancellationToken);
        db.ItemImages.Remove(image);
        item.Collection.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Slika predmeta je odstranjena.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var item = await FindOwnedItem(id, cancellationToken);
        return item is null ? NotFound() : View(item);
    }

    [Authorize]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var item = await FindOwnedItem(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        await imageStorage.DeleteAsync(item.PrimaryImagePath, cancellationToken);
        foreach (var image in item.Images)
        {
            await imageStorage.DeleteAsync(image.ImagePath, cancellationToken);
        }

        var collectionId = item.CollectionId;
        item.Collection.UpdatedUtc = DateTime.UtcNow;
        db.CollectibleItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Predmet je izbrisan.";
        return RedirectToAction("Details", "Collections", new { id = collectionId });
    }

    private async Task<Collection?> FindOwnedCollection(Guid id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return null;
        }

        return await db.Collections.FirstOrDefaultAsync(
            x => x.Id == id && x.CreatedByUserId == userId,
            cancellationToken);
    }

    private async Task<CollectibleItem?> FindOwnedItem(Guid id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return null;
        }

        return await db.CollectibleItems
            .Include(x => x.Collection)
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == id && x.Collection.CreatedByUserId == userId, cancellationToken);
    }

    private Task<bool> InternalCodeExists(Guid collectionId, string internalCode, Guid? exceptId, CancellationToken cancellationToken) =>
        db.CollectibleItems.AnyAsync(x =>
            x.CollectionId == collectionId &&
            x.InternalCode == internalCode &&
            (!exceptId.HasValue || x.Id != exceptId.Value), cancellationToken);

    private static string NewInternalCode() => $"ITM-{Guid.NewGuid():N}".ToUpperInvariant();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void FillCurrentMedia(ItemEditViewModel model, CollectibleItem item)
    {
        model.CurrentPrimaryImagePath = item.PrimaryImagePath;
        model.CurrentImages = item.Images.OrderBy(x => x.SortOrder).ToList();
    }
}
