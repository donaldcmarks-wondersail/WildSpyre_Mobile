using UnityEngine;

/// <summary>
/// One movement pattern a pickup can be given via SO_PickupData.behavior. Plain C# (not a
/// MonoBehaviour) so Pickup can create/own the strategy directly with no extra GameObjects or
/// components — see PickupMotions.cs for the concrete behaviors and Pickup.CreateMotion for the
/// mapping from PickupBehaviorType.
/// </summary>
public interface IPickupMotion
{
    /// <summary>Called whenever the pickup (re)activates, including every time it's reused from
    /// the pool, so per-run state (bob phase, spawn origin, etc.) always restarts clean.</summary>
    void Init(Transform pickupTransform, SO_PickupData data);

    /// <summary>Called from Pickup.Update — skipped for a frame where PickupMagnetize has taken
    /// over the transform instead (see Pickup.Update / PickupMagnetize.IsMagnetizing).</summary>
    void Tick(float deltaTime);
}
