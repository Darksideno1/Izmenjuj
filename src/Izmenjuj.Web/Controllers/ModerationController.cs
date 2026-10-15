using Izmenjuj.Web.Data;
using Izmenjuj.Web.Domain;
using Izmenjuj.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izmenjuj.Web.Controllers;

[Authorize]
public sealed class ModerationController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Report(string userId, Guid? tradeId, CancellationToken cancellationToken)
    {
        var currentUserId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(currentUserId) || currentUserId == userId) return NotFound();

        var target = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (target is null) return NotFound();
        if (tradeId.HasValue && !await IsTradeBetween(tradeId.Value, currentUserId, userId, cancellationToken)) return NotFound();

        return View(new UserReportInputViewModel
        {
            TargetUserId = target.Id,
            TargetDisplayName = DisplayName(target),
            TradeId = tradeId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(UserReportInputViewModel model, CancellationToken cancellationToken)
    {
        var currentUserId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(currentUserId) || currentUserId == model.TargetUserId) return NotFound();

        var target = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.TargetUserId, cancellationToken);
        if (target is null) return NotFound();
        model.TargetDisplayName = DisplayName(target);

        if (model.TradeId.HasValue && !await IsTradeBetween(model.TradeId.Value, currentUserId, model.TargetUserId, cancellationToken))
            return NotFound();

        model.Reason = model.Reason?.Trim() ?? string.Empty;
        model.Details = Clean(model.Details);
        if (!ModelState.IsValid) return View(model);

        var duplicateOpen = await db.UserReports.AsNoTracking().AnyAsync(x =>
            x.ReporterUserId == currentUserId
            && x.TargetUserId == model.TargetUserId
            && x.TradeId == model.TradeId
            && x.Status == UserReportStatus.Open,
            cancellationToken);

        if (duplicateOpen)
        {
            TempData["Success"] = "Za tega uporabnika je tvoja prijava že odprta.";
        }
        else
        {
            db.UserReports.Add(new UserReport
            {
                ReporterUserId = currentUserId,
                TargetUserId = model.TargetUserId,
                TradeId = model.TradeId,
                Reason = model.Reason,
                Details = model.Details
            });
            await db.SaveChangesAsync(cancellationToken);
            TempData["Success"] = "Prijava je poslana administratorju.";
        }

        return model.TradeId.HasValue
            ? RedirectToAction("Details", "Trades", new { id = model.TradeId.Value })
            : RedirectToAction("Index", "Matches");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Block(string userId, string? returnUrl, CancellationToken cancellationToken)
    {
        var currentUserId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(currentUserId) || currentUserId == userId) return NotFound();
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId, cancellationToken)) return NotFound();

        var exists = await db.UserBlocks.AnyAsync(x => x.BlockerUserId == currentUserId && x.BlockedUserId == userId, cancellationToken);
        if (!exists)
        {
            db.UserBlocks.Add(new UserBlock { BlockerUserId = currentUserId, BlockedUserId = userId });
            await db.SaveChangesAsync(cancellationToken);
        }

        TempData["Success"] = "Uporabnik je blokiran. Nova ujemanja in novi predlogi z njim se ne bodo prikazovali.";
        return RedirectLocalOrDefault(returnUrl, nameof(Blocks));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unblock(string userId, string? returnUrl, CancellationToken cancellationToken)
    {
        var currentUserId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(currentUserId)) return Challenge();

        var block = await db.UserBlocks.FirstOrDefaultAsync(x => x.BlockerUserId == currentUserId && x.BlockedUserId == userId, cancellationToken);
        if (block is not null)
        {
            db.UserBlocks.Remove(block);
            await db.SaveChangesAsync(cancellationToken);
        }

        TempData["Success"] = "Blokada je odstranjena.";
        return RedirectLocalOrDefault(returnUrl, nameof(Blocks));
    }

    [HttpGet]
    public async Task<IActionResult> Blocks(CancellationToken cancellationToken)
    {
        var currentUserId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(currentUserId)) return Challenge();

        var blocks = await db.UserBlocks.AsNoTracking()
            .Where(x => x.BlockerUserId == currentUserId)
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);
        var ids = blocks.Select(x => x.BlockedUserId).ToList();
        var users = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);

        return View(new BlockedUsersViewModel
        {
            Rows = blocks.Where(x => users.ContainsKey(x.BlockedUserId)).Select(x =>
            {
                var user = users[x.BlockedUserId];
                return new BlockedUserRowViewModel
                {
                    UserId = user.Id,
                    DisplayName = DisplayName(user),
                    City = user.City,
                    CreatedUtc = x.CreatedUtc
                };
            }).ToList()
        });
    }

    private Task<bool> IsTradeBetween(Guid tradeId, string currentUserId, string targetUserId, CancellationToken cancellationToken) =>
        db.Trades.AsNoTracking().AnyAsync(x => x.Id == tradeId
            && ((x.ProposerUserId == currentUserId && x.RecipientUserId == targetUserId)
                || (x.ProposerUserId == targetUserId && x.RecipientUserId == currentUserId)), cancellationToken);

    private IActionResult RedirectLocalOrDefault(string? returnUrl, string defaultAction)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction(defaultAction)!;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string DisplayName(ApplicationUser user) => string.IsNullOrWhiteSpace(user.DisplayName) ? (user.Email ?? "Uporabnik") : user.DisplayName;
}
