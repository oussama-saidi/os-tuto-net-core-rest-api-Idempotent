namespace OS.Tuto.IdempotentApi.Idempotency;

public class IdempotencyStore : IIdempotencyStore
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public IdempotencyStore(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<(bool found, int status, string body)?> TryGetAsync(string key, CancellationToken ct = default)
    {
        if (_cache.TryGetValue<(int, string)>(CacheKey(key), out var cached))
        {
            return (true, cached.Item1, cached.Item2);
        }

        var rec = await _db.IdempotencyRecords.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, ct);

        if (rec is null) return (false, 0, "");

        // honor expiry if set
        if (rec.ExpiresAtUtc is not null && rec.ExpiresAtUtc < DateTime.UtcNow)
        {
            return (false, 0, "");
        }

        _cache.Set(CacheKey(key), (rec.StatusCode, rec.ResponseBody),
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

        return (true, rec.StatusCode, rec.ResponseBody);
    }

    public async Task SaveAsync(string key, object response, int statusCode, string requestHash, TimeSpan ttl, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(response, _jsonOptions);

        var now = DateTime.UtcNow;
        var rec = new IdempotencyRecord
        {
            Key = key,
            RequestHash = requestHash,
            StatusCode = statusCode,
            ResponseBody = body,
            CreatedAtUtc = now,
            ExpiresAtUtc = ttl == TimeSpan.Zero ? null : now.Add(ttl)
        };

        _db.IdempotencyRecords.Add(rec);
        await _db.SaveChangesAsync(ct);

        _cache.Set(CacheKey(key), (statusCode, body),
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
    }

    public Task<string?> GetRequestHashAsync(string key, CancellationToken ct = default)
        => _db.IdempotencyRecords
              .Where(x => x.Key == key)
              .Select(x => x.RequestHash)
              .FirstOrDefaultAsync(ct);

    public Task<string> ComputeHashAsync<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Task.FromResult(Convert.ToHexString(hash));
    }

    private static string CacheKey(string key) => $"idem:{key}";
}
