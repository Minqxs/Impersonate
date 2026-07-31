using System.Collections.Concurrent;
using Impersonate.Application.Delivery;

namespace Impersonate.Infrastructure.Delivery;

internal sealed class DeliveryWorkspaceRegistry
{
    private readonly ConcurrentDictionary<string, string> paths = new(StringComparer.Ordinal);
    public DeliveryWorkspaceReference Register(string path)
    {
        var id = Guid.NewGuid().ToString("N");
        paths[id] = Path.GetFullPath(path);
        return new(id);
    }
    public string Resolve(DeliveryWorkspaceReference reference) => paths.TryGetValue(reference.Value, out var path) ? path : throw new InvalidOperationException("Delivery workspace is unavailable.");
    public void Remove(DeliveryWorkspaceReference reference) => paths.TryRemove(reference.Value, out _);
}
