namespace OS.Tuto.IdempotentApi.Domain;

public enum PaymentStatus
{
    Authorized = 0,
    Captured = 1,
    Failed = 2
}