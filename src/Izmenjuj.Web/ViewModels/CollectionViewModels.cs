using Izmenjuj.Web.Domain;
using System.ComponentModel.DataAnnotations;

namespace Izmenjuj.Web.ViewModels;

public sealed class CollectionEditViewModel
{
    public Guid Id { get; set; }

    [Required, StringLength(160, MinimumLength = 2), Display(Name = "Naslov zbirke")]
    public string Title { get; set; } = string.Empty;

    [StringLength(160), Display(Name = "Podnaslov")]
    public string? Subtitle { get; set; }

    [StringLength(4000), Display(Name = "Opis")]
    public string? Description { get; set; }

    [Range(1800, 2100), Display(Name = "Leto")]
    public int? Year { get; set; }

    [StringLength(160), Display(Name = "Proizvajalec / založnik")]
    public string? Manufacturer { get; set; }

    [StringLength(80), Display(Name = "Država")]
    public string? Country { get; set; }

    [Display(Name = "Tip zbirke")]
    public CollectionType Type { get; set; } = CollectionType.Stickers;

    [Display(Name = "Prikaži slike predmetov v seznamih")]
    public bool ShowItemImages { get; set; }

    [Display(Name = "Javno objavljena zbirka")]
    public bool IsPublished { get; set; } = true;

    [Display(Name = "Naslovna slika")]
    public IFormFile? CoverImage { get; set; }

    [Display(Name = "Dodatne slike")]
    public List<IFormFile> GalleryImages { get; set; } = [];

    public string? CurrentCoverImagePath { get; set; }
    public List<CollectionImage> CurrentImages { get; set; } = [];
}
