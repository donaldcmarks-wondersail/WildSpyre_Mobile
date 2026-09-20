using UnityEngine;

/// <summary>
/// Places exactly one fire stamp at the point of impact whenever the player bounces
/// off something while in Sling mode — routed to the ground trail's pool or the wall
/// trail's pool depending on how steep the impact surface is, via each emitter's
/// PlaceOneShotStamp (see FireTrailEmitterBase). Doesn't own a pool, a controller
/// prefab, or any spawn/spacing logic of its own — it's purely a classifier sitting
/// on top of the two existing emitters.
///
/// Must live on the same GameObject as the player's Rigidbody2D/Collider2D (the
/// CTRL_PlayerPlatformer root) — OnCollisionEnter2D only fires on components attached
/// to the colliding body, and Unity calls it on every one of them, so this sits
/// alongside CTRL_PlayerPlatformer's own OnCollisionEnter2D without needing to modify
/// that method (which only concerns itself with slingVars.canSling).
/// </summary>
public class FireTrailSlingEmitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CTRL_PlayerPlatformer _playerCtrl;
    [SerializeField] private FireTrailEmitter _groundEmitter;
    [SerializeField] private FireTrailWallEmitter _wallEmitter;

    [Header("Classification")]
    [Tooltip("Impact normals within this many degrees of straight up count as a floor hit (ground pool); anything steeper counts as a wall hit (wall pool). Mirrors how CTRL_PlayerPlatformer.slopeCheck classifies steepness, kept as its own independent value here.")]
    [SerializeField] private float _floorAngleThreshold = 45f;

    [Header("Filter")]
    [Tooltip("Layers that count as an impact worth marking. Falls back to the player's own ground mask if left as Nothing.")]
    [SerializeField] private LayerMask _impactMask;

    private void Start()
    {
        if (_impactMask.value == 0)
            _impactMask = _playerCtrl.groundCheck.groundMask;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_playerCtrl.PlayerState != CTRL_PlayerPlatformer.playerControlState.Sling) return;
        if (((1 << collision.gameObject.layer) & _impactMask.value) == 0) return;
        if (collision.contactCount == 0) return;

        ContactPoint2D contact = collision.GetContact(0);
        PlaceImpactStamp(contact.point, contact.normal);
    }

    /// <summary>
    /// Routes one stamp to the ground or wall emitter's pool based on how steep the given
    /// surface normal is. Public so other impact sources (e.g. PlayerProjectile) can reuse
    /// this classification and this emitter pair's existing scene wiring instead of
    /// duplicating either.
    /// </summary>
    public void PlaceImpactStamp(Vector2 point, Vector2 normal)
    {
        float angleFromUp = Vector2.Angle(normal, Vector2.up);

        if (angleFromUp <= _floorAngleThreshold)
            _groundEmitter.PlaceOneShotStamp(point, normal);
        else
            _wallEmitter.PlaceOneShotStamp(point, normal);
    }

    /// <summary>
    /// Places one stamp from the GROUND emitter's floor-authored prefab regardless of how steep
    /// the surface is, with the stamp rotated so its local up follows the surface normal. Unlike
    /// PlaceImpactStamp this deliberately skips the floor/wall split: the wall emitter uses a
    /// separately authored prefab (P_FireTrailWallController/Cell), so rotating it by the normal
    /// would come out wrong. Used by projectile impacts, which want one prefab on every surface.
    /// </summary>
    public void PlaceAlignedStamp(Vector2 point, Vector2 normal)
    {
        _groundEmitter.PlaceOneShotStamp(point, normal, alignToNormal: true);
    }
}
