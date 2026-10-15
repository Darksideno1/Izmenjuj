using Izmenjuj.Web.Domain;
using System.ComponentModel.DataAnnotations;

namespace Izmenjuj.Web.ViewModels;

public sealed class ItemEditViewModel
{
    public Guid Id { get; set; }
    public Guid CollectionId { get; set; }
    public string CollectionTitle { get; set; } = string.Empty;

    [Required, StringLength(160, MinimumLength = 1), Display(Name = "Naziv predmeta")]
    public string Name { get; set; } = string.Empty;

    [StringLength(80), Display(Name = "Uradna oznaka / številka")]
    public string? OfficialCode { get; set; }

    [StringLength(80), Display(Name = "Interna oznaka")]
    public string? InternalCode { get; set; }

    [StringLength(120), Display(Name = "Varianta")]
    public string? Variant { get; set; }

    [StringLength(2000), Display(Name = "Opis")]
    public string? Description { get; set; }

    [Display(Name = "Glavna slika")]
    public IFormFile? PrimaryImage { get; set; }

    [Display(Name = "Dodatne slike")]
    public List<IFormFile> GalleryImages { get; set; } = [];

    public string? CurrentPrimaryImagePath { get; set; }
    public List<ItemImage> CurrentImages { get; set; } = [];
}

public sealed class CollectionInventoryViewModel
{
    public required Collection Collection { get; set; }
    public List<InventoryRowViewModel> Rows { get; set; } = [];
    public int OwnedItems { get; set; }
    public int WantedItems { get; set; }
    public int ForTradeItems { get; set; }
    public int TotalItems { get; set; }
    public int CompletionPercent => TotalItems == 0 ? 0 : (int)Math.Round(OwnedItems * 100d / TotalItems);
}

public sealed class InventoryRowViewModel
{
    public Guid ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? OfficialCode { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public string? Variant { get; set; }
    public string? PrimaryImagePath { get; set; }

    [Range(0, 999), Display(Name = "Imam")]
    public int HaveQuantity { get; set; }

    [Display(Name = "Iščem")]
    public bool Wanted { get; set; }

    [Range(0, 999), Display(Name = "Za menjavo")]
    public int ForTradeQuantity { get; set; }
}
