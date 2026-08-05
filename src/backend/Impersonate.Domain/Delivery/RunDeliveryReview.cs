namespace Impersonate.Domain.Delivery;

public sealed class RunDeliveryReview
{
    private RunDeliveryReview()
    {
    }
    public Guid Id
    {
        get; private set;
    }
    public Guid RunDeliveryId
    {
        get; private set;
    }
    public int AttemptNumber
    {
        get; private set;
    }
    public string Provider { get; private set; } = null!;
    public string Model { get; private set; } = null!;
    public string ExactHeadSha { get; private set; } = null!;
    public DeliveryReviewDecision Decision
    {
        get; private set;
    }
    public string Summary { get; private set; } = null!;
    public string? Feedback
    {
        get; private set;
    }
    public string FindingsJson { get; private set; } = "[]";
    public DateTimeOffset CreatedAtUtc
    {
        get; private set;
    }
    public DateTimeOffset? SupersededAtUtc
    {
        get; private set;
    }
    public bool IsCurrent => SupersededAtUtc is null;
    public static RunDeliveryReview Create(Guid deliveryId, int attempt, string provider, string model, string head, DeliveryReviewDecision decision, string summary, string findings, string? feedback = null, DateTimeOffset? at = null)
    {
        if (deliveryId == Guid.Empty || attempt <= 0)
            throw new ArgumentException("Final review identity is required.");
        return new()
        {
            Id = Guid.NewGuid(),
            RunDeliveryId = deliveryId,
            AttemptNumber = attempt,
            Provider = Required(provider, 50),
            Model = Required(model, 200),
            ExactHeadSha = Required(head, 64),
            Decision = decision,
            Summary = Required(summary, 2000),
            Feedback = Optional(feedback, 4000),
            FindingsJson = Required(findings, 16000),
            CreatedAtUtc = at ?? DateTimeOffset.UtcNow
        };
    }
    public void Supersede(DateTimeOffset? at = null)
    {
        if (SupersededAtUtc is not null)
            throw new InvalidOperationException("Final review is already superseded.");
        SupersededAtUtc = at ?? DateTimeOffset.UtcNow;
    }
    private static string Required(string? value, int max)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result))
            throw new ArgumentException("Value is required.");
        if (result.Length > max)
            throw new ArgumentOutOfRangeException(nameof(value));
        return result;
    }
    private static string? Optional(string? value, int max)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result))
            return null;
        if (result.Length > max)
            throw new ArgumentOutOfRangeException(nameof(value));
        return result;
    }
}
