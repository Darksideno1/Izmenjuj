using Izmenjuj.Web.Data;
using Izmenjuj.Web.Domain;
using Izmenjuj.Web.Services;
using Izmenjuj.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izmenjuj.Web.Controllers;

public sealed class CollectionsController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IImageStorage imageStorage) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        var isAdmin = User.IsInRole("Admin");
        q = Clean(q);

        var query = db.Collections
            .AsNoTracking()
            .Include(x => x.Images)
            .Include(x => x.Items)
            .Where(x => x.IsPublished || isAdmin || (userId != null && x.CreatedByUserId == userId));

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x => x.Title.Contains(q)
                                     || (x.Subtitle != null && x.Subtitle.Contains(q))
                                     || (x.Manufacturer != null && x.Manufacturer.Contains(q))
                                     || x.Items.Any(i => i.Name.Contains(q)
                                                        || (i.OfficialCode != null && i.OfficialCode.Contains(q))
                                                        || i.InternalCode.Contains(q)));
        }

        ViewData["Query"] = q;
        return View(await query.OrderByDescending(x => x.UpdatedUtc).ToListAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        var isAdmin = User.IsInRole("Admin");
        var collection = await db.Collections
            .AsNoTracking()
            .Include(x => x.Images.OrderBy(i => i.SortOrder))
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (collection is null || (!collection.IsPublished && collection.CreatedByUserId != userId && !isAdmin))
        {
            return NotFound();
        }

        ViewData["CanEdit"] = isAdmin || (userId != null && collection.CreatedByUserId == userId);
        return View(collection);
    }

    [Authorize]
    [HttpGet]
    public IActionResult Create() => View("Edit", new CollectionEditViewModel());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CollectionEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Edit", model);
        }

        var userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var collection = new Collection
        {
            Title = model.Title.Trim(),
            Subtitle = Clean(model.Subtitle),
            Description = Clean(model.Description),
            Year = model.Year,
            Manufacturer = Clean(model.Manufacturer),
            Country = Clean(model.Country),
            Type = model.Type,
            IsPublished = model.IsPublished,
            CreatedByUserId = userId
        };

        try
        {
            if (model.CoverImage is not null)
            {
                collection.CoverImagePath = await imageStorage.SaveAsync(model.CoverImage, "collection-covers", cancellationToken);
            }

            var sortOrder = 0;
            foreach (var image in model.GalleryImages.Where(x => x.Length > 0))
            {
                collection.Images.Add(new CollectionImage
                {
                    ImagePath = await imageStorage.SaveAsync(image, "collection-gallery", cancellationToken),
                    SortOrder = sortOrder++
                });
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.CurrentImages = collection.Images;
            model.CurrentCoverImagePath = collection.CoverImagePath;
            return View("Edit", model);
        }

        db.Collections.Add(collection);
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Zbirka je ustvarjena.";
        return RedirectToAction(nameof(Details), new { id = collection.Id });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var collection = await FindManagedCollection(id, cancellationToken);
        if (collection is null)
        {
            return NotFound();
        }

        return View(new CollectionEditViewModel
        {
            Id = collection.Id,
            Title = collection.Title,
            Subtitle = collection.Subtitle,
            Description = collection.Description,
            Year = collection.Year,
            Manufacturer = collection.Manufacturer,
            Country = collection.Country,
            Type = collection.Type,
            IsPublished = collection.IsPublished,
            CurrentCoverImagePath = collection.CoverImagePath,
            CurrentImages = collection.Images.OrderBy(x => x.SortOrder).ToList()
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CollectionEditViewModel model, CancellationToken cancellationToken)
    {
        var collection = await FindManagedCollection(model.Id, cancellationToken);
        if (collection is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            model.CurrentCoverImagePath = collection.CoverImagePath;
            model.CurrentImages = collection.Images.OrderBy(x => x.SortOrder).ToList();
            return View(model);
        }

        collection.Title = model.Title.Trim();
        collection.Subtitle = Clean(model.Subtitle);
        collection.Description = Clean(model.Description);
        collection.Year = model.Year;
        collection.Manufacturer = Clean(model.Manufacturer);
        collection.Country = Clean(model.Country);
        collection.Type = model.Type;
        collection.IsPublished = model.IsPublished;
        collection.UpdatedUtc = DateTime.UtcNow;

        try
        {
            if (model.CoverImage is not null)
            {
                var oldCover = collection.CoverImagePath;
                collection.CoverImagePath = await imageStorage.SaveAsync(model.CoverImage, "collection-covers", cancellationToken);
                await imageStorage.DeleteAsync(oldCover, cancellationToken);
            }

            var sortOrder = collection.Images.Count == 0 ? 0 : collection.Images.Max(x => x.SortOrder) + 1;
            foreach (var image in model.GalleryImages.Where(x => x.Length > 0))
            {
                collection.Images.Add(new CollectionImage
                {
                    ImagePath = await imageStorage.SaveAsync(image, "collection-gallery", cancellationToken),
                    SortOrder = sortOrder++
                });
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.CurrentCoverImagePath = collection.CoverImagePath;
            model.CurrentImages = collection.Images.OrderBy(x => x.SortOrder).ToList();
            return View(model);
        }

        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Zbirka je shranjena.";
        return RedirectToAction(nameof(Details), new { id = collection.Id });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        var collection = await FindManagedCollection(id, cancellationToken);
        if (collection is null)
        {
            return NotFound();
        }

        var image = collection.Images.FirstOrDefault(x => x.Id == imageId);
        if (image is null)
        {
            return NotFound();
        }

        await imageStorage.DeleteAsync(image.ImagePath, cancellationToken);
        db.CollectionImages.Remove(image);
        collection.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Slika je odstranjena.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var collection = await FindManagedCollection(id, cancellationToken);
        return collection is null ? NotFound() : View(collection);
    }

    [Authorize]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var collection = await FindManagedCollection(id, cancellationToken);
        if (collection is null)
        {
            return NotFound();
        }

        var itemIds = collection.Items.Select(x => x.Id).ToList();
        if (itemIds.Count > 0 && (await db.UserInventories.AnyAsync(x => itemIds.Contains(x.CollectibleItemId), cancellationToken)
                                 || await db.TradeLines.AnyAsync(x => itemIds.Contains(x.CollectibleItemId), cancellationToken)))
        {
            TempData["Error"] = "Zbirke ni mogoče izbrisati, ker so njeni predmeti že uporabljeni v osebnih evidencah ali menjavah. Zbirko raje skrij.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await imageStorage.DeleteAsync(collection.CoverImagePath, cancellationToken);
        foreach (var image in collection.Images)
        {
            await imageStorage.DeleteAsync(image.ImagePath, cancellationToken);
        }

        foreach (var item in collection.Items)
        {
            await imageStorage.DeleteAsync(item.PrimaryImagePath, cancellationToken);
            foreach (var image in item.Images)
            {
                await imageStorage.DeleteAsync(image.ImagePath, cancellationToken);
            }
        }

        db.Collections.Remove(collection);
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Zbirka je izbrisana.";
        return User.IsInRole("Admin") ? RedirectToAction("Collections", "Admin") : RedirectToAction(nameof(Index));
    }

    private async Task<Collection?> FindManagedCollection(Guid id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return null;
        }

        var isAdmin = User.IsInRole("Admin");
        return await db.Collections
            .Include(x => x.Images)
            .Include(x => x.Items)
                .ThenInclude(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == id && (isAdmin || x.CreatedByUserId == userId), cancellationToken);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
