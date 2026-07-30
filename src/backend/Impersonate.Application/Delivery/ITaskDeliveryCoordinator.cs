using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public interface ITaskDeliveryCoordinator
{
    Task<DeliveryOperationResult<ApprovedTaskHandoff>> BuildHandoffAsync(Guid projectId, Guid runId, Guid taskId, CancellationToken cancellationToken);
    Task<DeliveryOperationResult<TaskDelivery>> GetOrCreateAsync(Guid projectId, Guid runId, Guid taskId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeliveryEligibility>> GetEligibilityAsync(Guid projectId, Guid runId, CancellationToken cancellationToken);
}
