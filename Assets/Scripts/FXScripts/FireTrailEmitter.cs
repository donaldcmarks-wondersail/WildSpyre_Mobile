using UnityEngine;

/// <summary>
/// Lives on the player and decides WHEN and WHERE the ground fire trail places a
/// stamp — grounded/moving gating, spacing, and the ground probe that makes stamps
/// conform to slopes. Owns no visuals or colliders itself; instead it checks a
/// FireTrailController out of a pool (see FireTrailEmitterBase) on each new grounding
/// event and hands it resolved world points via PlaceStamp().
///
/// Controllers are standalone GameObjects, NOT parented to the player, so a placed
/// stamp never gets dragged along afterward — and because leaving the ground clears
/// the "current" controller (the next one is a fresh pool checkout), dropping to a
/// different platform starts a brand new, disconnected trail instead of bridging back
/// to wherever the last one left off.
/// </summary>
public class FireTrailEmitter : FireTrailEmitterBase
{
    [Header("Ground Spawn")]
    [Tooltip("Below this HORIZONTAL speed, no new stamps place — deliberately ignores vertical velocity so a straight-up jump (no horizontal movement) doesn't read as \"running\" off the jump impulse's speed spike.")]
    [SerializeField] private float _minSpeedToTrail = 0.5f;

    [Header("Ground Probe")]
    [SerializeField] private float _groundProbeDistance = 0.5f;
    [SerializeField] private float _groundProbeSkin = 0.05f;

    private void Update()
    {
        bool grounded = _playerCtrl.PlayerState == CTRL_PlayerPlatformer.playerControlState.Platformer
                     && _playerCtrl.groundCheck.isGrounded;

        if (!grounded)
        {
            ReleaseCurrent();
            SetEmitting(false);
            return;
        }

        // "Emitting a trail" tracks the same grounded+speed gate that governs whether
        // a stamp *could* be placed — not whether one lands this exact frame (stamps
        // only land once every Spawn Interval, which would make the particles flicker
        // on for a frame and off for several rather than burn continuously).
        //
        // Horizontal speed only, deliberately — a straight-up jump has zero
        // horizontal velocity but a large vertical one from the jump impulse, and
        // groundCheck.isGrounded can still read stale-true for the frame the jump
        // fires (CTRL_PlayerPlatformer's ground check and jump both run in the same
        // FixedUpdate, ground check first). Using full velocity magnitude here would
        // let that vertical spike alone satisfy "moving fast enough to trail" and
        // drop a stamp right under a player who isn't running at all.
        bool isEmittingTrail = Mathf.Abs(_playerCtrl.rb.linearVelocity.x) >= _minSpeedToTrail;
        SetEmitting(isEmittingTrail);

        if (isEmittingTrail) TrySpawn();
    }

    // ── Spawning ─────────────────────────────────────────────────────────────
    private void TrySpawn()
    {
        // Speed gating already happened in Update() (it's also needed there to drive
        // particle emission), so this only has the spacing/placement work left to do.

        // Cheap per-frame distance check (no raycast) — bottom-center of the AABB is
        // close enough to the real foot position to gate spawning; the actual stamp
        // position below is found with a proper ground probe.
        Vector2 cheapFootPos = new Vector2(_playerCollider.bounds.center.x, _playerCollider.bounds.min.y);
        if (ShouldSkipSpacing(cheapFootPos)) return;

        if (_currentController == null)
        {
            _currentController = GetPooledController();
            if (_currentController == null) return;   // maxPooledControllers < 1 misconfiguration guard
        }

        TryGetGroundPoint(cheapFootPos, out Vector2 point, out Vector2 normal);
        _currentController.PlaceStamp(point, normal);

        _lastSpawnPos = cheapFootPos;
        _hasLastSpawn = true;
    }

    /// <summary>
    /// Finds the exact surface point/normal under the player right now — this, not
    /// the player's transform, is what makes stamps sit correctly on slopes and
    /// stairs with no slope-specific code.
    /// </summary>
    private bool TryGetGroundPoint(Vector2 footPos, out Vector2 point, out Vector2 normal)
    {
        Vector2 origin = footPos + Vector2.up * _groundProbeSkin;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, _groundProbeDistance + _groundProbeSkin, _groundMask);
        if (hit.collider != null)
        {
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        point = footPos;
        normal = Vector2.up;
        return false;
    }
}
