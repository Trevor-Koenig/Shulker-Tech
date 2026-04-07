using System.Text.Json;

namespace ShulkerTech.Core.Services;

public class MojangService(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Looks up a Minecraft username via the Mojang API.
    /// Returns (uuid, username) if found, null if the username doesn't exist.
    /// </summary>
    public async Task<MojangProfile?> GetProfileAsync(string username)
    {
        var response = await http.GetAsync(
            $"https://api.mojang.com/users/profiles/minecraft/{Uri.EscapeDataString(username)}");

        if (!response.IsSuccessStatusCode)
            return null;

        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MojangProfile>(body, JsonOptions);
    }
}

public record MojangProfile(string Id, string Name);
