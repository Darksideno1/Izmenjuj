using System.ComponentModel.DataAnnotations;

namespace Izmenjuj.Web.ViewModels;

public sealed class RegisterViewModel
{
    [Required, EmailAddress, Display(Name = "E-pošta")]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(80, MinimumLength = 2), Display(Name = "Prikazno ime")]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(80), Display(Name = "Kraj")]
    public string? City { get; set; }

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8), Display(Name = "Geslo")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Ponovi geslo")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Menjam po pošti")]
    public bool TradesByMail { get; set; } = true;

    [Display(Name = "Menjam osebno")]
    public bool TradesInPerson { get; set; } = true;
}

public sealed class LoginViewModel
{
    [Required, EmailAddress, Display(Name = "E-pošta")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Geslo")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Zapomni si me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class ForgotPasswordViewModel
{
    [Required, EmailAddress, Display(Name = "E-pošta")]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordViewModel
{
    [Required, EmailAddress, Display(Name = "E-pošta")]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8), Display(Name = "Novo geslo")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Ponovi novo geslo")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ProfileViewModel
{
    [Required, StringLength(80, MinimumLength = 2), Display(Name = "Prikazno ime")]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(80), Display(Name = "Kraj")]
    public string? City { get; set; }

    [Display(Name = "Menjam po pošti")]
    public bool TradesByMail { get; set; }

    [Display(Name = "Menjam osebno")]
    public bool TradesInPerson { get; set; }

    public string Email { get; set; } = string.Empty;
}
