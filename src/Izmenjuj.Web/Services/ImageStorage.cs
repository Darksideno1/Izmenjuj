using System.Net.Http.Headers;

namespace Izmenjuj.Web.Services;

public interface IImageStorage
{
    Task<string> SaveAsync(IFormFile file, string category, CancellationToken cancellationToken = default);
    Task DeleteAsync(string? path, CancellationToken cancellationToken = default);
}

public sealed class LocalImageStorage(IWebHostEnvironment environment) : IImageStorage
{
    private const long MaxFileSize = 6 * 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public async Task<string> SaveAsync(IFormFile file, string category, CancellationToken cancellationToken = default)
    {
        Validate(file);
        var extension = GetExtension(file);
        var safeCategory = string.Concat(category.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
        var relativeDirectory = Path.Combine("uploads", safeCategory);
        var absoluteDirectory = Path.Combine(environment.WebRootPath, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(absoluteDirectory, fileName);
        await using var stream = File.Create(absolutePath);
        await file.CopyToAsync(stream, cancellationToken);

        return "/" + Path.Combine(relativeDirectory, fileName).Replace('\\', '/');
    }

    public Task DeleteAsync(string? path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var relative = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var absolute = Path.GetFullPath(Path.Combine(environment.WebRootPath, relative));
        var root = Path.GetFullPath(Path.Combine(environment.WebRootPath, "uploads"));
        if (absolute.StartsWith(root, StringComparison.Ordinal) && File.Exists(absolute))
        {
            File.Delete(absolute);
        }

        return Task.CompletedTask;
    }

    private static void Validate(IFormFile file)
    {
        if (file.Length <= 0 || file.Length > MaxFileSize)
        {
            throw new InvalidOperationException("Slika mora biti manjša od 6 MB.");
        }
        if (!AllowedTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException("Dovoljene so slike JPG, PNG in WEBP.");
        }
    }

    private static string GetExtension(IFormFile file) => file.ContentType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg"
    };
}

public sealed class SupabaseImageStorage(HttpClient httpClient, IConfiguration configuration) : IImageStorage
{
    private const long MaxFileSize = 6 * 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private string BaseUrl => (configuration["Storage:SupabaseUrl"]
        ?? throw new InvalidOperationException("Storage:SupabaseUrl ni nastavljen.")).TrimEnd('/');
    private string SecretKey => configuration["Storage:SupabaseSecretKey"]
        ?? throw new InvalidOperationException("Storage:SupabaseSecretKey ni nastavljen.");
    private string Bucket => configuration["Storage:Bucket"] ?? "izmenjuj-images";

    public async Task<string> SaveAsync(IFormFile file, string category, CancellationToken cancellationToken = default)
    {
        Validate(file);
        var extension = file.ContentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg"
        };
        var safeCategory = string.Concat(category.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
        var objectPath = $"{safeCategory}/{Guid.NewGuid():N}{extension}";

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{BaseUrl}/storage/v1/object/{Bucket}/{objectPath}");
        AddAuthHeaders(request);
        request.Headers.TryAddWithoutValidation("x-upsert", "false");

        await using var input = file.OpenReadStream();
        request.Content = new StreamContent(input);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Shranjevanje slike ni uspelo ({(int)response.StatusCode}): {detail}");
        }

        return $"{BaseUrl}/storage/v1/object/public/{Bucket}/{objectPath}";
    }

    public async Task DeleteAsync(string? path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var prefix = $"{BaseUrl}/storage/v1/object/public/{Bucket}/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var objectPath = path[prefix.Length..];
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{BaseUrl}/storage/v1/object/{Bucket}/{objectPath}");
        AddAuthHeaders(request);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Brisanje slike iz shrambe ni uspelo.");
        }
    }

    private void AddAuthHeaders(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey);
        request.Headers.TryAddWithoutValidation("apikey", SecretKey);
    }

    private static void Validate(IFormFile file)
    {
        if (file.Length <= 0 || file.Length > MaxFileSize)
        {
            throw new InvalidOperationException("Slika mora biti manjša od 6 MB.");
        }
        if (!AllowedTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException("Dovoljene so slike JPG, PNG in WEBP.");
        }
    }
}
