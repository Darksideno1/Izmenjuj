using Izmenjuj.Web.Data;
using Izmenjuj.Web.Domain;
using Izmenjuj.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Izmenjuj.Web.Services;

public sealed class UserSafetyFilter(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var principal = context.HttpContext.User;
        var userId = userManager.GetUserId(principal);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var suspended = await db.UserModerations
                .AsNoTracking()
                .AnyAsync(x => x.UserId == userId && x.IsSuspended, context.HttpContext.RequestAborted);

            if (suspended)
            {
                await signInManager.SignOutAsync();
                context.Result = new RedirectToActionResult("Login", "Account", new { suspended = true });
                return;
            }

            if (context.ActionDescriptor is ControllerActionDescriptor descriptor
                && descriptor.ControllerName == "Trades"
                && descriptor.ActionName == "Create")
            {
                var otherUserId = ResolveTradeTarget(context);
                if (!string.IsNullOrWhiteSpace(otherUserId))
                {
                    var blocked = await db.UserBlocks.AsNoTracking().AnyAsync(x =>
                        (x.BlockerUserId == userId && x.BlockedUserId == otherUserId)
                        || (x.BlockerUserId == otherUserId && x.BlockedUserId == userId),
                        context.HttpContext.RequestAborted);

                    if (blocked)
                    {
                        context.Result = new ForbidResult();
                        return;
                    }
                }
            }
        }

        await next();
    }

    private static string? ResolveTradeTarget(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue("candidateUserId", out var candidate)
            && candidate is string candidateUserId)
        {
            return candidateUserId;
        }

        if (context.ActionArguments.TryGetValue("posted", out var posted)
            && posted is TradeEditViewModel trade)
        {
            return trade.OtherUserId;
        }

        return null;
    }
}
