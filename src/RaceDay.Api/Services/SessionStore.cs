using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace RaceDay.Api.Services;

// What the server keeps for each logged-in user
public record SessionInfo(string SessionId, int UserId, string Role, DateTime CreatedAt, DateTime ExpiresAt);

public interface IUserSessionStore
{
    Task<SessionInfo> CreateAsync(int userId, string role, CancellationToken ct = default);
    Task<SessionInfo?> GetAsync(string sessionId, CancellationToken ct = default);
    Task RemoveAsync(string sessionId, CancellationToken ct = default);
}

// Stores sessions in ASP.NET Core's distributed cache (in memory for now).
// Login creates a session, every request checks it, logout removes it.
public class DistributedCacheSessionStore : IUserSessionStore
{
    private const string KeyPrefix = "raceday:session:";
    private readonly IDistributedCache _cache;
    private readonly JwtSettings _settings;

    public DistributedCacheSessionStore(IDistributedCache cache, IOptions<JwtSettings> settings)
    {
        _cache = cache;
        _settings = settings.Value;
    }

    public async Task<SessionInfo> CreateAsync(int userId, string role, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var session = new SessionInfo(Guid.NewGuid().ToString("N"), userId, role, now, now.AddMinutes(_settings.ExpiryMinutes));

        // Session expires at the same time as the token
        await _cache.SetStringAsync(KeyPrefix + session.SessionId, JsonSerializer.Serialize(session),
            new DistributedCacheEntryOptions { AbsoluteExpiration = session.ExpiresAt }, ct);

        return session;
    }

    public async Task<SessionInfo?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;

        var json = await _cache.GetStringAsync(KeyPrefix + sessionId, ct);
        return json is null ? null : JsonSerializer.Deserialize<SessionInfo>(json);
    }

    public Task RemoveAsync(string sessionId, CancellationToken ct = default) =>
        _cache.RemoveAsync(KeyPrefix + sessionId, ct);
}