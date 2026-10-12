using UnityEngine;

/// <summary>
/// Shared contract for anything Ability_ThrowProjectile can launch — CTRL_EnemyProjectile (free
/// ballistic flight) and CTRL_SurfaceProjectile (flies free, then sticks to and crawls along a
/// surface) both implement this, so the ability doesn't need to know or care which one is on the
/// prefab it was handed.
/// </summary>
public interface IThrownProjectile
{
    void Launch(Vector2 direction, float speed);
}
