using UnityEngine;

/// <summary>
/// Interface for all enemy movement implementations.
/// States interact exclusively through this interface — no mover type awareness needed.
/// </summary>
public interface IEnemyMover
{
    /// <summary>Request movement toward a world-space position.</summary>
    void MoveTo(Vector2 worldTarget);

    /// <summary>Halt all movement.</summary>
    void Stop();

    /// <summary>True when the enemy has reached the current target position.</summary>
    bool IsAtTarget { get; }

    /// <summary>Override the movement speed (e.g. walk vs chase).</summary>
    void SetSpeed(float speed);

    /// <summary>
    /// Override the velocity-smoothing time for fly/climb movement (e.g. a floatier
    /// patrol vs. a snappier chase). Set alongside <see cref="SetSpeed"/>.
    /// </summary>
    void SetSmoothTime(float seconds);

    /// <summary>
    /// Sign of the last *intentional* horizontal travel direction (+1 right, -1 left,
    /// 0 if never moved horizontally). Only updated by path-following/jump movement —
    /// never by external pushes like knockback — so it's safe to drive facing/art
    /// flips from this without them reacting to hit reactions.
    /// </summary>
    float FacingX { get; }

    /// <summary>
    /// Overrides <see cref="FacingX"/> directly — used to face an attack target
    /// so that direction sticks as the movement-derived facing once the attack ends,
    /// instead of reverting to whatever direction was last walked.
    /// </summary>
    void SetFacing(float dirX);
}
