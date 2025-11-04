namespace OS.Tuto.IdempotentApi.Dtos;

public record PaymentResponse(Guid Id, decimal Amount, string Currency, string Recipient, string Status, DateTime CreatedAtUtc);
