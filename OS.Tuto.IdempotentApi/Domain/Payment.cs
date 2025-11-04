namespace OS.Tuto.IdempotentApi.Domain;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Recipient { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // To prevent duplicate rows for same operation:
    public string IdempotencyKey { get; set; } = default!;

    public PaymentStatus Status { get; set; } = PaymentStatus.Authorized;
}
