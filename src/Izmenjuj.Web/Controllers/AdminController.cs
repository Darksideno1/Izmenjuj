using Izmenjuj.Web.Data;
using Izmenjuj.Web.Domain;
using Izmenjuj.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izmenjuj.Web.Controllers;

[Authorize(Policy = "Admin")]
public sealed class AdminController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(new AdminDashboardViewModel
        {
            Users = await db.Users.CountAsync(cancellationToken),
            Collections = await db.Collections.CountAsync(cancellationToken),
            Items = await db.CollectibleItems.CountAsync(cancellationToken),
            ActiveTrades = await db.Trades.CountAsync(x => x.Status == TradeStatus.Proposed || x.Status == TradeStatus.Accepted, cancellationToken),
            OpenReports = await db.UserReports.CountAsync(x => x.Status == UserReportStatus.Open, cancellationToken),
            SuspendedUsers = await db.UserModerations.CountAsync(x => x.IsSuspended, cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Collections(string? q, bool? published, CancellationToken cancellationToken)
    {
        q = Clean(q);
        var query = db.Collections
            .AsNoTracking()
            .Include(x => x.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x => x.Title.Contains(q)
                                     || (x.Subtitle != null && x.Subtitle.Contains(q))
                                     || (x.Manufacturer != null && x.Manufacturer.Contains(q)));
        }

        if (published.HasValue)
        {
            query = query.Where(x => x.IsPublished == published.Value);
        }

        var collections = await query.OrderByDescending(x => x.UpdatedUtc).ToListAsync(cancellationToken);
        var ownerIds = collections.Where(x => x.CreatedByUserId != null).Select(x => x.CreatedByUserId!).Distinct().ToList();
        var owners = await db.Users.AsNoTracking()
            .Where(x => ownerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return View(new AdminCollectionsViewModel
        {
            Query = q,
            Published = published,
            Rows = collections.Select(x => new AdminCollectionRowViewModel
            {
                Id = x.Id,
                Title = x.Title,
                Subtitle = x.Subtitle,
                Type = x.Type,
                IsPublished = x.IsPublished,
                ItemCount = x.Items.Count,
                UpdatedUtc = x.UpdatedUtc,
                OwnerName = x.CreatedByUserId is not null && owners.TryGetValue(x.CreatedByUserId, out var owner)
                    ? DisplayName(owner)
                    : "Sistem"
            }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCollectionPublication(Guid id, CancellationToken cancellationToken)
    {
        var collection = await db.Collections.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (collection is null) return NotFound();

        collection.IsPublished = !collection.IsPublished;
        collection.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = collection.IsPublished ? "Zbirka je objavljena." : "Zbirka je skrita.";
        return RedirectToAction(nameof(Collections));
    }

    [HttpGet]
    public async Task<IActionResult> Users(string? q, CancellationToken cancellationToken)
    {
        q = Clean(q);
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x => x.DisplayName.Contains(q)
                                     || (x.Email != null && x.Email.Contains(q))
                                     || (x.City != null && x.City.Contains(q)));
        }

        var users = await query.OrderByDescending(x => x.CreatedUtc).Take(500).ToListAsync(cancellationToken);
        var ids = users.Select(x => x.Id).ToList();
        var moderations = await db.UserModerations.AsNoTracking()
            .Where(x => ids.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, cancellationToken);
        var reportCounts = await db.UserReports.AsNoTracking()
            .Where(x => ids.Contains(x.TargetUserId) && x.Status == UserReportStatus.Open)
            .GroupBy(x => x.TargetUserId)
            .Select(x => new { UserId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);
        var admins = (await userManager.GetUsersInRoleAsync("Admin")).Select(x => x.Id).ToHashSet();

        return View(new AdminUsersViewModel
        {
            Query = q,
            Rows = users.Select(x => new AdminUserRowViewModel
            {
                Id = x.Id,
                DisplayName = DisplayName(x),
                Email = x.Email ?? string.Empty,
                City = x.City,
                CreatedUtc = x.CreatedUtc,
                IsSuspended = moderations.TryGetValue(x.Id, out var moderation) && moderation.IsSuspended,
                IsAdmin = admins.Contains(x.Id),
                OpenReports = reportCounts.GetValueOrDefault(x.Id)
            }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetSuspended(AdminSuspendInputViewModel input, CancellationToken cancellationToken)
    {
        var currentUserId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(currentUserId) || currentUserId == input.UserId)
        {
            TempData["Error"] = "Lastnega administratorskega računa ne moreš suspendirati.";
            return RedirectToAction(nameof(Users));
        }

        var target = await userManager.FindByIdAsync(input.UserId);
        if (target is null) return NotFound();
        if (await userManager.IsInRoleAsync(target, "Admin"))
        {
            TempData["Error"] = "Administratorskega računa ni mogoče suspendirati iz tega pogleda.";
            return RedirectToAction(nameof(Users));
        }

        var moderation = await db.UserModerations.FirstOrDefaultAsync(x => x.UserId == target.Id, cancellationToken);
        if (moderation is null)
        {
            moderation = new UserModeration { UserId = target.Id };
            db.UserModerations.Add(moderation);
        }

        moderation.IsSuspended = input.Suspended;
        moderation.Note = Clean(input.Note);
        moderation.UpdatedByUserId = currentUserId;
        moderation.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await userManager.SetLockoutEnabledAsync(target, true);
        if (input.Suspended)
        {
            await userManager.SetLockoutEndDateAsync(target, new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero));
        }
        else
        {
            await userManager.SetLockoutEndDateAsync(target, null);
            await userManager.ResetAccessFailedCountAsync(target);
        }
        await userManager.UpdateSecurityStampAsync(target);

        TempData["Success"] = input.Suspended ? "Uporabnik je suspendiran." : "Suspenzija je odstranjena.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> Reports(UserReportStatus? status, CancellationToken cancellationToken)
    {
        var query = db.UserReports.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);

        var reports = await query.OrderByDescending(x => x.CreatedUtc).Take(500).ToListAsync(cancellationToken);
        var userIds = reports.SelectMany(x => new[] { x.ReporterUserId, x.TargetUserId }).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);

        return View(new AdminReportsViewModel
        {
            Status = status,
            Rows = reports.Select(x => new AdminReportRowViewModel
            {
                Id = x.Id,
                ReporterName = users.TryGetValue(x.ReporterUserId, out var reporter) ? DisplayName(reporter) : "Neznan uporabnik",
                TargetName = users.TryGetValue(x.TargetUserId, out var target) ? DisplayName(target) : "Neznan uporabnik",
                TargetUserId = x.TargetUserId,
                TradeId = x.TradeId,
                Reason = x.Reason,
                Details = x.Details,
                Status = x.Status,
                CreatedUtc = x.CreatedUtc,
                ResolutionNote = x.ResolutionNote
            }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveReport(AdminResolveReportViewModel input, CancellationToken cancellationToken)
    {
        var report = await db.UserReports.FirstOrDefaultAsync(x => x.Id == input.Id, cancellationToken);
        if (report is null) return NotFound();
        if (input.Status == UserReportStatus.Open)
        {
            TempData["Error"] = "Za obravnavano prijavo izberi rešeno ali zavrženo.";
            return RedirectToAction(nameof(Reports));
        }

        report.Status = input.Status;
        report.ResolutionNote = Clean(input.Note);
        report.ReviewedByUserId = userManager.GetUserId(User);
        report.ReviewedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Prijava je obravnavana.";
        return RedirectToAction(nameof(Reports));
    }

    [HttpGet]
    public async Task<IActionResult> Trades(TradeStatus? status, CancellationToken cancellationToken)
    {
        var query = db.Trades.AsNoTracking().Include(x => x.Lines).Include(x => x.Messages).AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);

        var trades = await query.OrderByDescending(x => x.CreatedUtc).Take(500).ToListAsync(cancellationToken);
        var userIds = trades.SelectMany(x => new[] { x.ProposerUserId, x.RecipientUserId }).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);

        return View(new AdminTradesViewModel
        {
            Status = status,
            Rows = trades.Select(x => new AdminTradeSummaryViewModel
            {
                Id = x.Id,
                ProposerName = users.TryGetValue(x.ProposerUserId, out var proposer) ? DisplayName(proposer) : "Neznan uporabnik",
                RecipientName = users.TryGetValue(x.RecipientUserId, out var recipient) ? DisplayName(recipient) : "Neznan uporabnik",
                Status = x.Status,
                LineQuantity = x.Lines.Sum(line => line.Quantity),
                MessageCount = x.Messages.Count,
                CreatedUtc = x.CreatedUtc
            }).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Trade(Guid id, CancellationToken cancellationToken)
    {
        var trade = await db.Trades.AsNoTracking()
            .Include(x => x.Lines).ThenInclude(x => x.Item).ThenInclude(x => x.Collection)
            .Include(x => x.Messages)
            .Include(x => x.Events)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (trade is null) return NotFound();

        var userIds = trade.Lines.Select(x => x.FromUserId)
            .Concat(trade.Messages.Select(x => x.SenderUserId))
            .Concat(trade.Events.Select(x => x.ActorUserId))
            .Append(trade.ProposerUserId)
            .Append(trade.RecipientUserId)
            .Distinct()
            .ToList();
        var users = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        string Name(string userId) => users.TryGetValue(userId, out var user) ? DisplayName(user) : "Neznan uporabnik";

        return View(new AdminTradeInspectViewModel
        {
            Id = trade.Id,
            ProposerName = Name(trade.ProposerUserId),
            RecipientName = Name(trade.RecipientUserId),
            Status = trade.Status,
            CreatedUtc = trade.CreatedUtc,
            Lines = trade.Lines.Select(x => new AdminTradeLineViewModel
            {
                FromUserName = Name(x.FromUserId),
                ItemLabel = $"{x.Item.OfficialCode ?? x.Item.InternalCode} – {x.Item.Name}",
                CollectionTitle = x.Item.Collection.Title,
                Quantity = x.Quantity
            }).ToList(),
            Messages = trade.Messages.OrderBy(x => x.SentUtc).Select(x => new AdminTradeMessageViewModel
            {
                SenderName = Name(x.SenderUserId),
                Body = x.Body,
                SentUtc = x.SentUtc
            }).ToList(),
            Events = trade.Events.OrderBy(x => x.CreatedUtc).Select(x => new AdminTradeEventViewModel
            {
                ActorName = Name(x.ActorUserId),
                Type = x.Type,
                Note = x.Note,
                CreatedUtc = x.CreatedUtc
            }).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> BulkItems(Guid collectionId, CancellationToken cancellationToken)
    {
        var collection = await db.Collections.AsNoTracking().FirstOrDefaultAsync(x => x.Id == collectionId, cancellationToken);
        if (collection is null) return NotFound();
        return View(new AdminBulkItemsViewModel { CollectionId = collection.Id, CollectionTitle = collection.Title });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkItems(AdminBulkItemsViewModel model, CancellationToken cancellationToken)
    {
        var collection = await db.Collections.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == model.CollectionId, cancellationToken);
        if (collection is null) return NotFound();
        model.CollectionTitle = collection.Title;

        if (string.IsNullOrWhiteSpace(model.Data))
        {
            ModelState.AddModelError(nameof(model.Data), "Vnesi vsaj eno vrstico.");
            return View(model);
        }

        var byCode = collection.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.OfficialCode))
            .GroupBy(x => x.OfficialCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var raw in model.Data.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var parts = SplitImportLine(line);
            if (parts.Length < 2)
            {
                skipped++;
                continue;
            }

            var code = parts[0].Trim();
            var name = parts[1].Trim();
            var variant = parts.Length > 2 ? Clean(parts[2]) : null;
            if (code.Length is 0 or > 80 || name.Length is 0 or > 160 || (variant?.Length ?? 0) > 120)
            {
                skipped++;
                continue;
            }

            if (byCode.TryGetValue(code, out var existing))
            {
                existing.Name = name;
                existing.Variant = variant;
                updated++;
            }
            else
            {
                var item = new CollectibleItem
                {
                    CollectionId = collection.Id,
                    OfficialCode = code,
                    InternalCode = NewInternalCode(),
                    Name = name,
                    Variant = variant
                };
                collection.Items.Add(item);
                byCode[code] = item;
                added++;
            }
        }

        collection.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        model.ResultMessage = $"Dodano: {added}, posodobljeno: {updated}, preskočeno: {skipped}.";
        model.Data = string.Empty;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> MergeItems(Guid collectionId, CancellationToken cancellationToken)
    {
        var model = await BuildMergeModel(collectionId, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MergeItems(AdminMergeItemsViewModel model, CancellationToken cancellationToken)
    {
        if (model.SourceItemId == model.TargetItemId)
        {
            ModelState.AddModelError(string.Empty, "Izvorni in ciljni predmet morata biti različna.");
            return View(await BuildMergeModel(model.CollectionId, cancellationToken) ?? model);
        }

        var source = await db.CollectibleItems.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == model.SourceItemId, cancellationToken);
        var target = await db.CollectibleItems.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == model.TargetItemId, cancellationToken);
        if (source is null || target is null || source.CollectionId != target.CollectionId || source.CollectionId != model.CollectionId)
            return NotFound();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var sourceInventories = await db.UserInventories.Where(x => x.CollectibleItemId == source.Id).ToListAsync(cancellationToken);
            var targetInventories = await db.UserInventories.Where(x => x.CollectibleItemId == target.Id).ToListAsync(cancellationToken);
            var inventoryMap = targetInventories.ToDictionary(x => (x.UserId, x.State));

            foreach (var sourceInventory in sourceInventories)
            {
                if (inventoryMap.TryGetValue((sourceInventory.UserId, sourceInventory.State), out var targetInventory))
                {
                    targetInventory.Quantity = sourceInventory.State == InventoryState.Wanted
                        ? Math.Max(targetInventory.Quantity, sourceInventory.Quantity)
                        : targetInventory.Quantity + sourceInventory.Quantity;
                    db.UserInventories.Remove(sourceInventory);
                }
                else
                {
                    sourceInventory.CollectibleItemId = target.Id;
                }
            }

            var sourceLines = await db.TradeLines.Where(x => x.CollectibleItemId == source.Id).ToListAsync(cancellationToken);
            var targetLines = await db.TradeLines.Where(x => x.CollectibleItemId == target.Id).ToListAsync(cancellationToken);
            var lineMap = targetLines.GroupBy(x => (x.TradeId, x.FromUserId)).ToDictionary(x => x.Key, x => x.First());
            foreach (var sourceLine in sourceLines)
            {
                if (lineMap.TryGetValue((sourceLine.TradeId, sourceLine.FromUserId), out var targetLine))
                {
                    targetLine.Quantity += sourceLine.Quantity;
                    db.TradeLines.Remove(sourceLine);
                }
                else
                {
                    sourceLine.CollectibleItemId = target.Id;
                }
            }

            var nextSort = target.Images.Count == 0 ? 0 : target.Images.Max(x => x.SortOrder) + 1;
            if (!string.IsNullOrWhiteSpace(source.PrimaryImagePath))
            {
                if (string.IsNullOrWhiteSpace(target.PrimaryImagePath))
                {
                    target.PrimaryImagePath = source.PrimaryImagePath;
                }
                else if (!string.Equals(target.PrimaryImagePath, source.PrimaryImagePath, StringComparison.OrdinalIgnoreCase))
                {
                    target.Images.Add(new ItemImage { ImagePath = source.PrimaryImagePath, SortOrder = nextSort++ });
                }
                source.PrimaryImagePath = null;
            }

            foreach (var image in source.Images.ToList())
            {
                source.Images.Remove(image);
                image.CollectibleItemId = target.Id;
                image.Item = target;
                image.SortOrder = nextSort++;
                target.Images.Add(image);
            }

            db.CollectibleItems.Remove(source);
            var collection = await db.Collections.FirstAsync(x => x.Id == target.CollectionId, cancellationToken);
            collection.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        TempData["Success"] = "Dvojnika sta združena. Evidence, menjave in slike so prenesene na ciljni predmet.";
        return RedirectToAction(nameof(Collections));
    }

    private async Task<AdminMergeItemsViewModel?> BuildMergeModel(Guid collectionId, CancellationToken cancellationToken)
    {
        var collection = await db.Collections.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == collectionId, cancellationToken);
        if (collection is null) return null;

        return new AdminMergeItemsViewModel
        {
            CollectionId = collection.Id,
            CollectionTitle = collection.Title,
            Items = collection.Items
                .OrderBy(x => x.OfficialCode ?? x.InternalCode)
                .Select(x => new AdminItemOptionViewModel
                {
                    Id = x.Id,
                    Label = $"{x.OfficialCode ?? x.InternalCode} – {x.Name}{(string.IsNullOrWhiteSpace(x.Variant) ? string.Empty : $" ({x.Variant})")}" 
                }).ToList()
        };
    }

    private static string[] SplitImportLine(string line)
    {
        if (line.Contains(';')) return line.Split(';');
        if (line.Contains('\t')) return line.Split('\t');
        return line.Split('|');
    }

    private static string NewInternalCode() => $"ITM-{Guid.NewGuid():N}".ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string DisplayName(ApplicationUser user) => string.IsNullOrWhiteSpace(user.DisplayName) ? (user.Email ?? "Uporabnik") : user.DisplayName;
}
