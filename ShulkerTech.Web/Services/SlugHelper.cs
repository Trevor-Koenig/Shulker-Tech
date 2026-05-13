using System.Text.RegularExpressions;

namespace ShulkerTech.Web.Services;

public static class SlugHelper
{
    public static async Task<string> GenerateUniqueSlugAsync(string name, Func<string, Task<bool>> exists)
    {
        var baseSlug = Regex.Replace(name.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');
        var slug = baseSlug;
        var i = 2;
        while (await exists(slug))
            slug = $"{baseSlug}-{i++}";
        return slug;
    }
}
