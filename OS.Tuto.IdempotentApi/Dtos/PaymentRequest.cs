namespace OS.Tuto.IdempotentApi.Dtos;

public record PaymentRequest(decimal Amount, string Currency, string Recipient);
