using NewsAgent.Models;

namespace NewsAgent.Delivery;

/// <summary>
/// Delivers a generated digest to recipients.
/// </summary>
public interface IDigestDelivery
{
    /// <summary>
    /// Delivers the digest asynchronously.
    /// </summary>
    Task DeliverAsync(DigestOutput digest, CancellationToken cancellationToken = default);
}
