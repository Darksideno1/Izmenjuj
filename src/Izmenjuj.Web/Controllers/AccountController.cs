using Izmenjuj.Web.Data;
using Izmenjuj.Web.Domain;
using Izmenjuj.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izmenjuj.Web.Controllers;

public sealed class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext db,
    IConfiguration configuration) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Profile));
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Profile));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = model.DisplayName.Trim(),
            City = string.IsNullOrWhiteSpace(model.City) ? null : model.City.Trim(),
            TradesByMail = model.TradesByMail,
            TradesInPerson = model.TradesInPerson
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        var adminEmail = configuration["Admin:Email"]?.Trim();
        if (!string.IsNullOrWhiteSpace(adminEmail)
            && string.Equals(adminEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            await userManager.AddToRoleAsync(user, "Admin");
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        TempData["Success"] = "Račun je ustvarjen. Dobrodošel na Izmenjuj.si!";
        return LocalRedirect(returnUrl ?? Url.Action("Index", "Collections")!);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null, bool suspended = false)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Profile));
        }

        ViewData["Suspended"] = suspended;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null && await db.UserModerations.AsNoTracking()
                .AnyAsync(x => x.UserId == user.Id && x.IsSuspended))
        {
            ModelState.AddModelError(string.Empty, "Ta račun je administrativno suspendiran.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.IsLockedOut
                ? "Račun je zaradi več neuspelih poskusov začasno zaklenjen."
                : "E-pošta ali geslo ni pravilno.");
            return View(model);
        }

        return LocalRedirect(model.ReturnUrl ?? Url.Action("Index", "Home")!);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        return View(new ProfileViewModel
        {
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            City = user.City,
            TradesByMail = user.TradesByMail,
            TradesInPerson = user.TradesInPerson
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        model.Email = user.Email ?? string.Empty;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        user.DisplayName = model.DisplayName.Trim();
        user.City = string.IsNullOrWhiteSpace(model.City) ? null : model.City.Trim();
        user.TradesByMail = model.TradesByMail;
        user.TradesInPerson = model.TradesInPerson;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        TempData["Success"] = "Profil je shranjen.";
        return RedirectToAction(nameof(Profile));
    }
}
