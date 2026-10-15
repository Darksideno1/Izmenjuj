using System.ComponentModel.DataAnnotations;

namespace Izmenjuj.Web.ViewModels;

public sealed class DeleteAccountViewModel
{
    [Required, DataType(DataType.Password), Display(Name = "Trenutno geslo")]
    public string Password { get; set; } = string.Empty;

    [Range(typeof(bool), "true", "true", ErrorMessage = "Potrditi moraš, da želiš izbris računa.")]
    [Display(Name = "Razumem, da je dejanje nepovratno")]
    public bool Confirm { get; set; }
}
