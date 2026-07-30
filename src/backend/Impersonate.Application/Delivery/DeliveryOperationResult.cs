namespace Impersonate.Application.Delivery;

public sealed record DeliveryOperationResult<T>(bool Succeeded, T? Value, string? Code, string? Error)
{
    public static DeliveryOperationResult<T> Ok(T value) => new(true, value, null, null);
    public static DeliveryOperationResult<T> Fail(string code, string error) => new(false, default, code, error);
}
