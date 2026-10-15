using Izmenjuj.Web.Data;
using Izmenjuj.Web.Domain;
using Izmenjuj.Web.Services;
using Izmenjuj.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izmenjuj.Web.Controllers;

[Authorize]
public sealed class MatchesController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IMatchingService matchingService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        Guid? collectionId,
        string? city,
        TradeModeFilter tradeMode = TradeModeFilter.Any,
        CancellationToken cancellationToken = default)
    {
        var me = await userManager.GetUserAsync(User);
        if (me is null)
        {
            return Challenge();
        }

        var model = new MatchIndexViewModel
        {
            CollectionId = collectionId,
            City = Clean(city),
            TradeMode = tradeMode,
            Collections = await db.Collections
                .AsNoTracking()
                .Where(x => (x.IsPublished || x.CreatedByUserId == me.Id) && x.Items.Any())
                .OrderBy(x => x.Title)
                .Select(x => new MatchCollectionOptionViewModel { Id = x.Id, Title = x.Title })
                .ToListAsync(cancellationToken)
        };

        var myQuery = db.UserInventories
            .AsNoTracking()
            .Include(x => x.Item)
                .ThenInclude(x => x.Collection)
            .Where(x => x.UserId == me.Id
                        && x.Quantity > 0
                        && (x.State == InventoryState.Wanted || x.State == InventoryState.ForTrade));

        if (collectionId is not null)
        {
            myQuery = myQuery.Where(x => x.Item.CollectionId == collectionId.Value);
        }

        var myRows = await myQuery.ToListAsync(cancellationToken);
        var myWanted = myRows
            .Where(x => x.State == InventoryState.Wanted)
            .Select(x => x.CollectibleItemId)
            .ToHashSet();
        var myForTrade = myRows
            .Where(x => x.State == InventoryState.ForTrade)
            .Select(x => x.CollectibleItemId)
            .ToHashSet();

        model.MyWantedCount = myWanted.Count;
        model.MyForTradeCount = myForTrade.Count;

        if (myWanted.Count == 0 || myForTrade.Count == 0)
        {
            return View(model);
        }

        var candidateQuery = db.UserInventories
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Item)
                .ThenInclude(x => x.Collection)
            .Where(x => x.UserId != me.Id
                        && x.Quantity > 0
                        && x.Item.Collection.IsPublished
                        && (x.State == InventoryState.Wanted || x.State == InventoryState.ForTrade));

        if (collectionId is not null)
        {
            candidateQuery = candidateQuery.Where(x => x.Item.CollectionId == collectionId.Value);
        }

        var candidateRows = await candidateQuery.ToListAsync(cancellationToken);
        var candidateGroups = candidateRows
            .GroupBy(x => x.UserId)
            .Where(group => IsCandidateAllowed(me, group.First().User, model.City, tradeMode))
            .ToDictionary(group => group.Key, group => group.ToList());

        var candidateInputs = candidateGroups.Select(pair => new MatchInput(
            pair.Key,
            pair.Value
                .Where(x => x.State == InventoryState.Wanted)
                .Select(x => x.CollectibleItemId)
                .ToHashSet(),
            pair.Value
                .Where(x => x.State == InventoryState.ForTrade)
                .Select(x => x.CollectibleItemId)
                .ToHashSet()));

        var results = matchingService.FindMatches(
            new MatchInput(me.Id, myWanted, myForTrade),
            candidateInputs);

        var myItems = myRows.ToDictionary(x => x.CollectibleItemId, x => x.Item);

        foreach (var result in results)
        {
            if (!candidateGroups.TryGetValue(result.UserId, out var rows) || rows.Count == 0)
            {
                continue;
            }

            var candidate = rows[0].User;
            var candidateItems = rows.ToDictionary(x => x.CollectibleItemId, x => x.Item);

            var match = new UserMatchViewModel
            {
                UserId = candidate.Id,
                DisplayName = string.IsNullOrWhiteSpace(candidate.DisplayName) ? "Zbiratelj" : candidate.DisplayName,
                City = candidate.City,
                TradesByMail = candidate.TradesByMail && me.TradesByMail,
                TradesInPerson = candidate.TradesInPerson && me.TradesInPerson,
                SameCity = !string.IsNullOrWhiteSpace(me.City)
                           && !string.IsNullOrWhiteSpace(candidate.City)
                           && string.Equals(me.City.Trim(), candidate.City.Trim(), StringComparison.OrdinalIgnoreCase),
                Score = result.Score,
                BalancedTradeCount = result.BalancedTradeCount,
                YouGive = result.YouGive
                    .Where(myItems.ContainsKey)
                    .Select(id => ToItem(myItems[id]))
                    .OrderBy(x => x.CollectionTitle)
                    .ThenBy(x => x.OfficialCode ?? x.InternalCode)
                    .ToList(),
                YouReceive = result.YouReceive
                    .Where(candidateItems.ContainsKey)
                    .Select(id => ToItem(candidateItems[id]))
                    .OrderBy(x => x.CollectionTitle)
                    .ThenBy(x => x.OfficialCode ?? x.InternalCode)
                    .ToList()
            };

            model.Matches.Add(match);
        }

        model.Matches = model.Matches
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.SameCity)
            .ThenByDescending(x => x.BalancedTradeCount)
            .ToList();

        return View(model);
    }

    private static bool IsCandidateAllowed(
        ApplicationUser me,
        ApplicationUser candidate,
        string? city,
        TradeModeFilter tradeMode)
    {
        var canMail = me.TradesByMail && candidate.TradesByMail;
        var canMeet = me.TradesInPerson && candidate.TradesInPerson;

        if (!canMail && !canMeet)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(city)
            && (string.IsNullOrWhiteSpace(candidate.City)
                || !candidate.City.Contains(city, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return tradeMode switch
        {
            TradeModeFilter.Mail => canMail,
            TradeModeFilter.InPerson => canMeet,
            _ => true
        };
    }

    private static MatchItemViewModel ToItem(CollectibleItem item) => new()
    {
        ItemId = item.Id,
        CollectionId = item.CollectionId,
        CollectionTitle = item.Collection.Title,
        Name = item.Name,
        OfficialCode = item.OfficialCode,
        InternalCode = item.InternalCode,
        Variant = item.Variant,
        PrimaryImagePath = item.PrimaryImagePath
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
