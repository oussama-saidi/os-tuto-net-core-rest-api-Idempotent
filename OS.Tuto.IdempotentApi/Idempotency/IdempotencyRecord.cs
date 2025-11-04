namespace OS.Tuto.IdempotentApi.Idempotency;

public class IdempotencyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = default!;            // The Idempotency-Key header
    public string RequestHash { get; set; } = default!;    // SHA256 of request payload
    public int StatusCode { get; set; }                    // Stored response code
    public string ResponseBody { get; set; } = default!;   // Stored response JSON
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }            // Optional TTL
}
