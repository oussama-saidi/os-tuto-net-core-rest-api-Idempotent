namespace OS.Tuto.IdempotentApi.Idempotency;

public interface IIdempotencyStore
{
    Task<(bool found, int status, string body)?> TryGetAsync(string key, CancellationToken ct = default);
    Task SaveAsync(string key, object response, int statusCode, string requestHash, TimeSpan ttl, CancellationToken ct = default);
    Task<string> ComputeHashAsync<T>(T payload);
    Task<string?> GetRequestHashAsync(string key, CancellationToken ct = default);

}
