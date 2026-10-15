using Izmenjuj.Web.Data;
using Izmenjuj.Web.Domain;
using Izmenjuj.Web.Services;
using Izmenjuj.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace Izmenjuj.Web.Controllers;

public sealed class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext db,
    IConfiguration configuration,
    IAppEmailSender emailSender,
    IOptions<IdentityOptions> identityOptionsAccessor) : Controller
{
    private readonly IdentityOptions _identityOptions = identityOptionsAccessor.Value;

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
    [EnableRateLimiting("auth")]
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

        if (_identityOptions.SignIn.RequireConfirmedAccount && !emailSender.IsConfigured)
        {
            ModelState.AddModelError(string.Empty, "Registracija trenutno ni na voljo, ker pošiljanje potrditvene e-pošte ni konfigurirano.");
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

        if (_identityOptions.SignIn.RequireConfirmedAccount)
        {
            try
            {
                await SendConfirmationEmail(user, cancellationToken: HttpContext.RequestAborted);
            }
            catch
            {
                await userManager.DeleteAsync(user);
                ModelState.AddModelError(string.Empty, "Potrditvenega sporočila ni bilo mogoče poslati. Poskusi ponovno pozneje.");
                return View(model);
            }

            TempData["Success"] = "Račun je ustvarjen. Na e-pošto smo poslali povezavo za potrditev računa.";
            return RedirectToAction(nameof(Login));
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        TempData["Success"] = "Račun je ustvarjen. Dobrodošel na Izmenjuj.si!";
        return LocalRedirect(returnUrl ?? Url.Action("Index", "Collections")!);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? token)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
        {
            return View("ConfirmEmail", false);
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return View("ConfirmEmail", false);
        }

        try
        {
            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            var result = await userManager.ConfirmEmailAsync(user, decodedToken);
            return View("ConfirmEmail", result.Succeeded);
        }
        catch (FormatException)
        {
            return View("ConfirmEmail", false);
        }
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendConfirmation(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vnesi veljaven e-poštni naslov.";
            return RedirectToAction(nameof(Login));
        }

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is not null && !await userManager.IsEmailConfirmedAsync(user) && emailSender.IsConfigured)
        {
            await SendConfirmationEmail(user, HttpContext.RequestAborted);
        }

        TempData["Success"] = "Če račun obstaja in še ni potrjen, smo poslali novo potrditveno povezavo.";
        return RedirectToAction(nameof(Login));
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
    [EnableRateLimiting("auth")]
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
            var message = result.IsLockedOut
                ? "Račun je zaradi več neuspelih poskusov začasno zaklenjen."
                : result.IsNotAllowed
                    ? "Račun še ni potrjen. Preveri e-pošto ali zahtevaj novo potrditveno povezavo."
                    : "E-pošta ali geslo ni pravilno.";
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        return LocalRedirect(model.ReturnUrl ?? Url.Action("Index", "Home")!);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is not null && await userManager.IsEmailConfirmedAsync(user) && emailSender.IsConfigured)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var callback = Url.Action(nameof(ResetPassword), "Account", new
            {
                email = user.Email,
                token = encodedToken
            }, Request.Scheme)!;

            await emailSender.SendAsync(
                user.Email!,
                "Ponastavitev gesla – Izmenjuj.si",
                $"<p>Za ponastavitev gesla klikni spodnjo povezavo:</p><p><a href=\"{WebUtility.HtmlEncode(callback)}\">Ponastavi geslo</a></p><p>Če zahteve nisi poslal ti, sporočilo prezri.</p>",
                HttpContext.RequestAborted);
        }

        return View("ForgotPasswordConfirmation");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            return View("ResetPasswordConfirmation");
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
        }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, "Povezava za ponastavitev gesla ni veljavna.");
            return View(model);
        }

        var result = await userManager.ResetPasswordAsync(user, decodedToken, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        return View("ResetPasswordConfirmation");
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
    [EnableRateLimiting("write")]
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

    private async Task SendConfirmationEmail(ApplicationUser user, CancellationToken cancellationToken)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var callback = Url.Action(nameof(ConfirmEmail), "Account", new
        {
            userId = user.Id,
            token = encodedToken
        }, Request.Scheme)!;

        await emailSender.SendAsync(
            user.Email!,
            "Potrdi račun – Izmenjuj.si",
            $"<p>Dobrodošel na Izmenjuj.si.</p><p>Za aktivacijo računa klikni spodnjo povezavo:</p><p><a href=\"{WebUtility.HtmlEncode(callback)}\">Potrdi e-poštni naslov</a></p>",
            cancellationToken);
    }
}
