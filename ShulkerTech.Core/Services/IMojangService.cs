namespace ShulkerTech.Core.Services;

public interface IMojangService
{
    Task<MojangProfile?> GetProfileAsync(string username);
}
