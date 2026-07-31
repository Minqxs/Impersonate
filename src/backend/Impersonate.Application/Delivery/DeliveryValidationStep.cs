namespace Impersonate.Application.Delivery;

public sealed record DeliveryValidationStep(string Name, bool Succeeded, string SafeSummary);
