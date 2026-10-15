using Izmenjuj.Web.Data;
using Izmenjuj.Web.Domain;
using Izmenjuj.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Izmenjuj.Web.Controllers;

[Authorize]
public sealed class PrivacyController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> ExportData(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var inventory = await db.UserInventories.AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .Include(x => x.Item).ThenInclude(x => x.Collection)
            .Select(x => new
            {
                x.Item.Collection.Title,
                x.Item.Name,
                x.Item.OfficialCode,
                x.Item.InternalCode,
                State = x.State.ToString(),
                x.Quantity,
                x.Notes
            })
            .ToListAsync(cancellationToken);

        var trades = await db.Trades.AsNoTracking()
            .Where(x => x.ProposerUserId == user.Id || x.RecipientUserId == user.Id)
            .Include(x => x.Lines).ThenInclude(x => x.Item)
            .Include(x => x.Messages)
            .Include(x => x.Events)
            .Include(x => x.Ratings)
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);

        var reports = await db.UserReports.AsNoTracking()
            .Where(x => x.ReporterUserId == user.Id || x.TargetUserId == user.Id)
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);

        var export = new
        {
            generatedUtc = DateTime.UtcNow,
            profile = new
            {
                user.Id,
                user.Email,
                user.DisplayName,
                user.City,
                user.TradesByMail,
                user.TradesInPerson,
                user.CreatedUtc,
                user.EmailConfirmed
            },
            inventory,
            trades = trades.Select(t => new
            {
                t.Id,
                status = t.Status.ToString(),
                t.CreatedUtc,
                t.ProposerUserId,
                t.RecipientUserId,
                lines = t.Lines.Select(l => new { l.Item.Name, l.Item.OfficialCode, l.FromUserId, l.Quantity }),
                messages = t.Messages.Select(m => new { m.SenderUserId, m.Body, m.SentUtc }),
                events = t.Events.Select(e => new { type = e.Type.ToString(), e.ActorUserId, e.Note, e.CreatedUtc }),
                ratings = t.Ratings.Select(r => new { r.FromUserId, r.ToUserId, r.Stars, r.Comment, r.CreatedUtc })
            }),
            reports = reports.Select(r => new
            {
                r.Id,
                r.ReporterUserId,
                r.TargetUserId,
                r.TradeId,
                r.Reason,
                r.Details,
                status = r.Status.ToString(),
                r.CreatedUtc,
                r.ResolutionNote
            })
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(export, new JsonSerializerOptions { WriteIndented = true });
        return File(bytes, "application/json", $"izmenjuj-podatki-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
    }

    [HttpGet]
    public IActionResult DeleteAccount() => View(new DeleteAccountViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountViewModel model, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (await userManager.IsInRoleAsync(user, "Admin"))
        {
            ModelState.AddModelError(string.Empty, "Administratorskega računa ni mogoče izbrisati iz uporabniškega vmesnika. Najprej prenesi administratorsko vlogo na drug račun.");
            return View(model);
        }

        if (!await userManager.CheckPasswordAsync(user, model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "Geslo ni pravilno.");
            return View(model);
        }

        var hasActiveTrades = await db.Trades.AnyAsync(
            x => (x.ProposerUserId == user.Id || x.RecipientUserId == user.Id)
                 && (x.Status == TradeStatus.Proposed || x.Status == TradeStatus.Accepted),
            cancellationToken);
        if (hasActiveTrades)
        {
            ModelState.AddModelError(string.Empty, "Računa ni mogoče izbrisati, dokler imaš odprto ali sprejeto menjavo. Najprej jo zaključi ali prekliči.");
            return View(model);
        }

        var oldUserId = user.Id;
        var anonymousId = $"deleted:{Guid.NewGuid():N}";

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.UserInventories.RemoveRange(await db.UserInventories.Where(x => x.UserId == oldUserId).ToListAsync(cancellationToken));
            db.UserBlocks.RemoveRange(await db.UserBlocks.Where(x => x.BlockerUserId == oldUserId || x.BlockedUserId == oldUserId).ToListAsync(cancellationToken));
            db.UserModerations.RemoveRange(await db.UserModerations.Where(x => x.UserId == oldUserId).ToListAsync(cancellationToken));

            await db.Collections.Where(x => x.CreatedByUserId == oldUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedByUserId, anonymousId), cancellationToken);
            await db.Trades.Where(x => x.ProposerUserId == oldUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ProposerUserId, anonymousId), cancellationToken);
            await db.Trades.Where(x => x.RecipientUserId == oldUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RecipientUserId, anonymousId), cancellationToken);
            await db.TradeLines.Where(x => x.FromUserId == oldUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.FromUserId, anonymousId), cancellationToken);
            await db.TradeMessages.Where(x => x.SenderUserId == oldUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.SenderUserId, anonymousId), cancellationToken);
            await db.TradeEvents.Where(x => x.ActorUserId == oldUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActorUserId, anonymousId), cancellationToken);
            await db.TradeRatings.Where(x => x.FromUserId == oldUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.FromUserId, anonymousId), cancellationToken);
            await db.TradeRatings.Where(x => x.ToUserId == oldUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ToUserId, anonymousId), cancellationToken);
            await db.UserReports.Where(x => x.ReporterUserId == oldUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReporterUserId, anonymousId), cancellationToken);
            await db.UserReports.Where(x => x.TargetUserId == oldUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.TargetUserId, anonymousId), cancellationToken);
            await db.UserReports.Where(x => x.ReviewedByUserId == oldUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReviewedByUserId, anonymousId), cancellationToken);
            await db.UserModerations.Where(x => x.UpdatedByUserId == oldUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedByUserId, anonymousId), cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
                await transaction.RollbackAsync(cancellationToken);
                return View(model);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await signInManager.SignOutAsync();
        return View("AccountDeleted");
    }
}
