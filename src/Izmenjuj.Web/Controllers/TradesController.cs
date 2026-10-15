using Izmenjuj.Web.Data;
using Izmenjuj.Web.Domain;
using Izmenjuj.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izmenjuj.Web.Controllers;

[Authorize]
public sealed class TradesController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var trades = await db.Trades
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.ProposerUserId == userId || x.RecipientUserId == userId)
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);

        var otherIds = trades
            .Select(x => x.ProposerUserId == userId ? x.RecipientUserId : x.ProposerUserId)
            .Distinct()
            .ToList();
        var users = await db.Users
            .AsNoTracking()
            .Where(x => otherIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var summaries = trades.Select(trade =>
        {
            var otherId = trade.ProposerUserId == userId ? trade.RecipientUserId : trade.ProposerUserId;
            users.TryGetValue(otherId, out var other);
            return new TradeSummaryViewModel
            {
                Id = trade.Id,
                OtherDisplayName = DisplayName(other),
                OtherCity = other?.City,
                Status = trade.Status,
                CreatedUtc = trade.CreatedUtc,
                YouGiveCount = trade.Lines.Where(x => x.FromUserId == userId).Sum(x => x.Quantity),
                YouReceiveCount = trade.Lines.Where(x => x.FromUserId != userId).Sum(x => x.Quantity)
            };
        }).ToList();

        return View(new TradeIndexViewModel
        {
            Active = summaries.Where(x => x.Status is TradeStatus.Proposed or TradeStatus.Accepted).ToList(),
            History = summaries.Where(x => x.Status is TradeStatus.Completed or TradeStatus.Rejected or TradeStatus.Cancelled).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(string candidateUserId, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var model = await BuildOfferModel(userId, candidateUserId, cancellationToken);
        return model is null ? NotFound() : View("Edit", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TradeEditViewModel posted, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var model = await BuildOfferModel(userId, posted.OtherUserId, cancellationToken);
        if (model is null) return NotFound();
        ApplySelections(model, posted);

        if (!ValidateOffer(model)) return View("Edit", model);

        var trade = new Trade
        {
            ProposerUserId = userId,
            RecipientUserId = posted.OtherUserId,
            Status = TradeStatus.Proposed
        };
        AddLines(trade, userId, posted.OtherUserId, model);
        trade.Events.Add(new TradeEvent { ActorUserId = userId, Type = TradeEventType.Proposed });
        AddOptionalMessage(trade, userId, posted.Message);

        db.Trades.Add(trade);
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Predlog menjave je poslan.";
        return RedirectToAction(nameof(Details), new { id = trade.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Counter(Guid id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var trade = await db.Trades
            .AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.Events)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (!IsParticipant(trade, userId) || trade!.Status != TradeStatus.Proposed) return NotFound();

        var lastActor = LastOfferActor(trade);
        if (lastActor == userId) return RedirectToAction(nameof(Details), new { id });

        var otherId = OtherUserId(trade, userId);
        var model = await BuildOfferModel(userId, otherId, cancellationToken);
        if (model is null) return NotFound();

        model.TradeId = trade.Id;
        model.IsCounter = true;
        PreselectExistingOffer(model, trade, userId, otherId);
        return View("Edit", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Counter(TradeEditViewModel posted, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null || posted.TradeId is null) return Challenge();

        var trade = await db.Trades
            .Include(x => x.Lines)
            .Include(x => x.Events)
            .FirstOrDefaultAsync(x => x.Id == posted.TradeId.Value, cancellationToken);
        if (!IsParticipant(trade, userId) || trade!.Status != TradeStatus.Proposed) return NotFound();
        if (LastOfferActor(trade) == userId) return RedirectToAction(nameof(Details), new { id = trade.Id });

        var otherId = OtherUserId(trade, userId);
        var model = await BuildOfferModel(userId, otherId, cancellationToken);
        if (model is null) return NotFound();
        model.TradeId = trade.Id;
        model.IsCounter = true;
        ApplySelections(model, posted);

        if (!ValidateOffer(model)) return View("Edit", model);

        db.TradeLines.RemoveRange(trade.Lines);
        trade.Lines.Clear();
        AddLines(trade, userId, otherId, model);
        trade.Events.Add(new TradeEvent { ActorUserId = userId, Type = TradeEventType.Countered });
        AddOptionalMessage(trade, userId, posted.Message);

        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Protipredlog je poslan.";
        return RedirectToAction(nameof(Details), new { id = trade.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var trade = await LoadTrade(id, cancellationToken);
        if (!IsParticipant(trade, userId)) return NotFound();

        var participantIds = new[] { trade!.ProposerUserId, trade.RecipientUserId };
        var users = await db.Users.AsNoTracking()
            .Where(x => participantIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var otherId = OtherUserId(trade, userId);
        users.TryGetValue(otherId, out var other);

        var completionActors = trade.Events
            .Where(x => x.Type == TradeEventType.CompletionConfirmed)
            .Select(x => x.ActorUserId)
            .ToHashSet();

        var model = new TradeDetailsViewModel
        {
            Id = trade.Id,
            Status = trade.Status,
            CreatedUtc = trade.CreatedUtc,
            CurrentUserId = userId,
            OtherUserId = otherId,
            OtherDisplayName = DisplayName(other),
            OtherCity = other?.City,
            IsProposer = trade.ProposerUserId == userId,
            LastOfferActorUserId = LastOfferActor(trade),
            YouGive = trade.Lines.Where(x => x.FromUserId == userId).Select(ToLine).ToList(),
            YouReceive = trade.Lines.Where(x => x.FromUserId != userId).Select(ToLine).ToList(),
            YouConfirmedCompletion = completionActors.Contains(userId),
            OtherConfirmedCompletion = completionActors.Contains(otherId),
            YouRated = trade.Ratings.Any(x => x.FromUserId == userId)
        };

        model.Messages = trade.Messages.OrderBy(x => x.SentUtc).Select(x => new TradeMessageViewModel
        {
            Id = x.Id,
            SenderUserId = x.SenderUserId,
            SenderName = users.TryGetValue(x.SenderUserId, out var sender) ? DisplayName(sender) : "Uporabnik",
            Body = x.Body,
            SentUtc = x.SentUtc
        }).ToList();

        model.Events = trade.Events.OrderByDescending(x => x.CreatedUtc).Select(x => new TradeEventViewModel
        {
            Type = x.Type,
            ActorName = users.TryGetValue(x.ActorUserId, out var actor) ? DisplayName(actor) : "Sistem",
            Note = x.Note,
            CreatedUtc = x.CreatedUtc
        }).ToList();

        model.Ratings = trade.Ratings.OrderBy(x => x.CreatedUtc).Select(x => new TradeRatingViewModel
        {
            FromDisplayName = users.TryGetValue(x.FromUserId, out var rater) ? DisplayName(rater) : "Uporabnik",
            Stars = x.Stars,
            Comment = x.Comment,
            CreatedUtc = x.CreatedUtc
        }).ToList();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var trade = await LoadTrade(id, cancellationToken, tracking: true);
        if (!IsParticipant(trade, userId) || trade!.Status != TradeStatus.Proposed) return NotFound();
        if (LastOfferActor(trade) == userId) return RedirectToAction(nameof(Details), new { id });

        if (!await TradeInventoryIsAvailable(trade, cancellationToken))
        {
            TempData["Error"] = "Predloga ni mogoče sprejeti, ker eden od predmetov ni več na voljo za menjavo.";
            return RedirectToAction(nameof(Details), new { id });
        }

        trade.Status = TradeStatus.Accepted;
        trade.Events.Add(new TradeEvent { ActorUserId = userId, Type = TradeEventType.Accepted });
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Predlog je sprejet. Po izvedeni menjavi morata zaključek potrditi oba.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();
        var trade = await LoadTrade(id, cancellationToken, tracking: true);
        if (!IsParticipant(trade, userId) || trade!.Status != TradeStatus.Proposed || LastOfferActor(trade) == userId) return NotFound();

        trade.Status = TradeStatus.Rejected;
        trade.Events.Add(new TradeEvent { ActorUserId = userId, Type = TradeEventType.Rejected });
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Predlog je zavrnjen.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();
        var trade = await LoadTrade(id, cancellationToken, tracking: true);
        if (!IsParticipant(trade, userId) || trade!.Status != TradeStatus.Proposed || LastOfferActor(trade) != userId) return NotFound();

        trade.Status = TradeStatus.Cancelled;
        trade.Events.Add(new TradeEvent { ActorUserId = userId, Type = TradeEventType.Cancelled });
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Predlog je preklican.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Message(TradeMessageInputViewModel input, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();
        var trade = await db.Trades.FirstOrDefaultAsync(x => x.Id == input.TradeId, cancellationToken);
        if (!IsParticipant(trade, userId)) return NotFound();

        var body = input.Body?.Trim();
        if (string.IsNullOrWhiteSpace(body) || body.Length > 2000)
        {
            TempData["Error"] = "Sporočilo mora vsebovati med 1 in 2000 znaki.";
            return RedirectToAction(nameof(Details), new { id = input.TradeId });
        }

        db.TradeMessages.Add(new TradeMessage { TradeId = input.TradeId, SenderUserId = userId, Body = body });
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { id = input.TradeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmComplete(Guid id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var locked = await db.Trades
                .Where(x => x.Id == id
                            && x.Status == TradeStatus.Accepted
                            && (x.ProposerUserId == userId || x.RecipientUserId == userId))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, x => x.Status), cancellationToken);
            if (locked == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return RedirectToAction(nameof(Details), new { id });
            }

            db.ChangeTracker.Clear();
            var trade = await LoadTrade(id, cancellationToken, tracking: true);
            if (!IsParticipant(trade, userId) || trade!.Status != TradeStatus.Accepted)
            {
                await transaction.RollbackAsync(cancellationToken);
                return NotFound();
            }

            if (!await TradeInventoryIsAvailable(trade, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["Error"] = "Menjave ni mogoče zaključiti, ker predlagana količina ni več na voljo.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!trade.Events.Any(x => x.Type == TradeEventType.CompletionConfirmed && x.ActorUserId == userId))
                trade.Events.Add(new TradeEvent { ActorUserId = userId, Type = TradeEventType.CompletionConfirmed });

            var otherId = OtherUserId(trade, userId);
            var otherConfirmed = trade.Events.Any(x => x.Type == TradeEventType.CompletionConfirmed && x.ActorUserId == otherId);
            if (!otherConfirmed)
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                TempData["Success"] = "Tvoja potrditev je shranjena. Čakamo še drugega udeleženca.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await ApplyCompletedTradeToInventory(trade, cancellationToken);
            trade.Status = TradeStatus.Completed;
            trade.Events.Add(new TradeEvent { ActorUserId = userId, Type = TradeEventType.Completed });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        TempData["Success"] = "Menjava je zaključena. Osebna evidenca obeh uporabnikov je posodobljena.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rate(TradeRatingInputViewModel input, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();
        var trade = await db.Trades.Include(x => x.Ratings).FirstOrDefaultAsync(x => x.Id == input.TradeId, cancellationToken);
        if (!IsParticipant(trade, userId) || trade!.Status != TradeStatus.Completed) return NotFound();
        if (trade.Ratings.Any(x => x.FromUserId == userId)) return RedirectToAction(nameof(Details), new { id = input.TradeId });

        var comment = Clean(input.Comment);
        if (input.Stars is < 1 or > 5 || (comment?.Length ?? 0) > 1000)
        {
            TempData["Error"] = "Ocena mora biti med 1 in 5, komentar pa ima lahko največ 1000 znakov.";
            return RedirectToAction(nameof(Details), new { id = input.TradeId });
        }

        trade.Ratings.Add(new TradeRating
        {
            FromUserId = userId,
            ToUserId = OtherUserId(trade, userId),
            Stars = input.Stars,
            Comment = comment
        });
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Ocena je shranjena.";
        return RedirectToAction(nameof(Details), new { id = input.TradeId });
    }

    private async Task<TradeEditViewModel?> BuildOfferModel(string userId, string otherUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(otherUserId) || otherUserId == userId) return null;
        var other = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == otherUserId, cancellationToken);
        if (other is null) return null;

        var inventories = await db.UserInventories
            .AsNoTracking()
            .Include(x => x.Item).ThenInclude(x => x.Collection)
            .Where(x => (x.UserId == userId || x.UserId == otherUserId) && x.Quantity > 0)
            .ToListAsync(cancellationToken);

        var myWanted = inventories
            .Where(x => x.UserId == userId && x.State == InventoryState.Wanted)
            .ToDictionary(x => x.CollectibleItemId, x => x.Quantity);
        var theirWanted = inventories
            .Where(x => x.UserId == otherUserId && x.State == InventoryState.Wanted)
            .ToDictionary(x => x.CollectibleItemId, x => x.Quantity);
        var myHave = inventories
            .Where(x => x.UserId == userId && x.State == InventoryState.Have)
            .ToDictionary(x => x.CollectibleItemId, x => x.Quantity);
        var theirHave = inventories
            .Where(x => x.UserId == otherUserId && x.State == InventoryState.Have)
            .ToDictionary(x => x.CollectibleItemId, x => x.Quantity);

        var mine = inventories
            .Where(x => x.UserId == userId && x.State == InventoryState.ForTrade && theirWanted.ContainsKey(x.CollectibleItemId))
            .Where(x => myHave.TryGetValue(x.CollectibleItemId, out var q) && q > 0)
            .Select(x => ToOfferItem(x, Math.Min(Math.Min(x.Quantity, myHave[x.CollectibleItemId]), theirWanted[x.CollectibleItemId])))
            .Where(x => x.MaxQuantity > 0)
            .OrderBy(x => x.CollectionTitle).ThenBy(x => x.Code)
            .ToList();

        var theirs = inventories
            .Where(x => x.UserId == otherUserId && x.State == InventoryState.ForTrade && myWanted.ContainsKey(x.CollectibleItemId))
            .Where(x => theirHave.TryGetValue(x.CollectibleItemId, out var q) && q > 0)
            .Select(x => ToOfferItem(x, Math.Min(Math.Min(x.Quantity, theirHave[x.CollectibleItemId]), myWanted[x.CollectibleItemId])))
            .Where(x => x.MaxQuantity > 0)
            .OrderBy(x => x.CollectionTitle).ThenBy(x => x.Code)
            .ToList();

        return new TradeEditViewModel
        {
            OtherUserId = otherUserId,
            OtherDisplayName = DisplayName(other),
            MyItems = mine,
            TheirItems = theirs
        };
    }

    private static TradeOfferItemViewModel ToOfferItem(UserInventory inventory, int maxQuantity) => new()
    {
        ItemId = inventory.CollectibleItemId,
        Name = inventory.Item.Name,
        Code = inventory.Item.OfficialCode ?? inventory.Item.InternalCode,
        CollectionTitle = inventory.Item.Collection.Title,
        Variant = inventory.Item.Variant,
        PrimaryImagePath = inventory.Item.PrimaryImagePath,
        MaxQuantity = maxQuantity,
        Quantity = 1
    };

    private void ApplySelections(TradeEditViewModel target, TradeEditViewModel posted)
    {
        var myPosted = posted.MyItems.GroupBy(x => x.ItemId).ToDictionary(x => x.Key, x => x.First());
        var theirPosted = posted.TheirItems.GroupBy(x => x.ItemId).ToDictionary(x => x.Key, x => x.First());
        foreach (var item in target.MyItems)
        {
            if (!myPosted.TryGetValue(item.ItemId, out var source)) continue;
            item.Selected = source.Selected;
            item.Quantity = source.Quantity;
        }
        foreach (var item in target.TheirItems)
        {
            if (!theirPosted.TryGetValue(item.ItemId, out var source)) continue;
            item.Selected = source.Selected;
            item.Quantity = source.Quantity;
        }
        target.Message = posted.Message;
    }

    private bool ValidateOffer(TradeEditViewModel model)
    {
        var mine = model.MyItems.Where(x => x.Selected).ToList();
        var theirs = model.TheirItems.Where(x => x.Selected).ToList();
        if (mine.Count == 0 || theirs.Count == 0)
            ModelState.AddModelError(string.Empty, "Izberi vsaj en predmet na vsaki strani menjave.");

        foreach (var item in mine.Concat(theirs))
        {
            if (item.Quantity < 1 || item.Quantity > item.MaxQuantity)
                ModelState.AddModelError(string.Empty, $"Količina za {item.Code} mora biti med 1 in {item.MaxQuantity}.");
        }
        return ModelState.IsValid;
    }

    private static void AddLines(Trade trade, string userId, string otherUserId, TradeEditViewModel model)
    {
        foreach (var item in model.MyItems.Where(x => x.Selected))
            trade.Lines.Add(new TradeLine { CollectibleItemId = item.ItemId, FromUserId = userId, Quantity = item.Quantity });
        foreach (var item in model.TheirItems.Where(x => x.Selected))
            trade.Lines.Add(new TradeLine { CollectibleItemId = item.ItemId, FromUserId = otherUserId, Quantity = item.Quantity });
    }

    private static void AddOptionalMessage(Trade trade, string userId, string? message)
    {
        var body = Clean(message);
        if (body is not null)
            trade.Messages.Add(new TradeMessage { SenderUserId = userId, Body = body.Length <= 2000 ? body : body[..2000] });
    }

    private static void PreselectExistingOffer(TradeEditViewModel model, Trade trade, string userId, string otherId)
    {
        var mine = trade.Lines.Where(x => x.FromUserId == userId).GroupBy(x => x.CollectibleItemId).ToDictionary(x => x.Key, x => x.First());
        var theirs = trade.Lines.Where(x => x.FromUserId == otherId).GroupBy(x => x.CollectibleItemId).ToDictionary(x => x.Key, x => x.First());
        foreach (var item in model.MyItems)
            if (mine.TryGetValue(item.ItemId, out var line)) { item.Selected = true; item.Quantity = Math.Min(line.Quantity, item.MaxQuantity); }
        foreach (var item in model.TheirItems)
            if (theirs.TryGetValue(item.ItemId, out var line)) { item.Selected = true; item.Quantity = Math.Min(line.Quantity, item.MaxQuantity); }
    }

    private async Task<Trade?> LoadTrade(Guid id, CancellationToken cancellationToken, bool tracking = false)
    {
        var query = db.Trades
            .Include(x => x.Lines).ThenInclude(x => x.Item).ThenInclude(x => x.Collection)
            .Include(x => x.Messages)
            .Include(x => x.Events)
            .Include(x => x.Ratings)
            .AsQueryable();
        if (!tracking) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private async Task<bool> TradeInventoryIsAvailable(Trade trade, CancellationToken cancellationToken)
    {
        var itemIds = trade.Lines.Select(x => x.CollectibleItemId).Distinct().ToList();
        var participantIds = new[] { trade.ProposerUserId, trade.RecipientUserId };
        var rows = await db.UserInventories.AsNoTracking()
            .Where(x => participantIds.Contains(x.UserId) && itemIds.Contains(x.CollectibleItemId)
                        && (x.State == InventoryState.Have || x.State == InventoryState.ForTrade))
            .ToListAsync(cancellationToken);

        foreach (var line in trade.Lines)
        {
            var have = rows.FirstOrDefault(x => x.UserId == line.FromUserId && x.CollectibleItemId == line.CollectibleItemId && x.State == InventoryState.Have)?.Quantity ?? 0;
            var forTrade = rows.FirstOrDefault(x => x.UserId == line.FromUserId && x.CollectibleItemId == line.CollectibleItemId && x.State == InventoryState.ForTrade)?.Quantity ?? 0;
            if (have < line.Quantity || forTrade < line.Quantity) return false;
        }
        return true;
    }

    private async Task ApplyCompletedTradeToInventory(Trade trade, CancellationToken cancellationToken)
    {
        var itemIds = trade.Lines.Select(x => x.CollectibleItemId).Distinct().ToList();
        var participantIds = new[] { trade.ProposerUserId, trade.RecipientUserId };
        var rows = await db.UserInventories
            .Where(x => participantIds.Contains(x.UserId) && itemIds.Contains(x.CollectibleItemId))
            .ToListAsync(cancellationToken);

        foreach (var line in trade.Lines)
        {
            var from = line.FromUserId;
            var to = from == trade.ProposerUserId ? trade.RecipientUserId : trade.ProposerUserId;
            var fromHave = rows.First(x => x.UserId == from && x.CollectibleItemId == line.CollectibleItemId && x.State == InventoryState.Have);
            var fromTrade = rows.First(x => x.UserId == from && x.CollectibleItemId == line.CollectibleItemId && x.State == InventoryState.ForTrade);

            fromHave.Quantity -= line.Quantity;
            fromTrade.Quantity -= line.Quantity;
            if (fromHave.Quantity <= 0) db.UserInventories.Remove(fromHave);
            if (fromTrade.Quantity <= 0) db.UserInventories.Remove(fromTrade);

            var wanted = rows.FirstOrDefault(x => x.UserId == to && x.CollectibleItemId == line.CollectibleItemId && x.State == InventoryState.Wanted);
            if (wanted is not null)
            {
                db.UserInventories.Remove(wanted);
                rows.Remove(wanted);
            }

            var toHave = rows.FirstOrDefault(x => x.UserId == to && x.CollectibleItemId == line.CollectibleItemId && x.State == InventoryState.Have);
            if (toHave is null)
            {
                toHave = new UserInventory { UserId = to, CollectibleItemId = line.CollectibleItemId, State = InventoryState.Have, Quantity = line.Quantity };
                db.UserInventories.Add(toHave);
                rows.Add(toHave);
            }
            else
            {
                toHave.Quantity += line.Quantity;
            }
        }
    }

    private static TradeLineViewModel ToLine(TradeLine line) => new()
    {
        ItemId = line.CollectibleItemId,
        Name = line.Item.Name,
        Code = line.Item.OfficialCode ?? line.Item.InternalCode,
        CollectionTitle = line.Item.Collection.Title,
        Variant = line.Item.Variant,
        PrimaryImagePath = line.Item.PrimaryImagePath,
        Quantity = line.Quantity
    };

    private static bool IsParticipant(Trade? trade, string userId) => trade is not null && (trade.ProposerUserId == userId || trade.RecipientUserId == userId);
    private static string OtherUserId(Trade trade, string userId) => trade.ProposerUserId == userId ? trade.RecipientUserId : trade.ProposerUserId;
    private static string? LastOfferActor(Trade trade) => trade.Events.Where(x => x.Type is TradeEventType.Proposed or TradeEventType.Countered).OrderByDescending(x => x.CreatedUtc).Select(x => x.ActorUserId).FirstOrDefault();
    private static string DisplayName(ApplicationUser? user) => user is null || string.IsNullOrWhiteSpace(user.DisplayName) ? "Zbiratelj" : user.DisplayName;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
