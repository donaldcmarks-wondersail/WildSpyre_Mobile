using UnityEngine;

/// <summary>
/// Wall-hang counterpart to FireTrailEmitter — same framework (FireTrailEmitterBase),
/// different trigger and probe direction. Lives on the player and decides WHEN and
/// WHERE the wall fire trail places a stamp: active for the entire duration of
/// groundCheck.isWallHanging, including the very first instant the player grabs the
/// wall (before any downward slide has actually started) — there's no minimum-speed
/// gate here, unlike the ground emitter, because "just grabbed, not sliding yet" is
/// explicitly part of what should show fire. Also stays active through
/// PlayerState.OnLedge — the scripted climb-over-the-corner state a wall-hang
/// transitions into — so the trail doesn't cut out right as the climb begins. Unlike
/// the continuous wall-slide case, OnLedge places exactly one stamp, right when the
/// climb is *initiated* — the player's position is held static for the whole climb
/// (CTRL_PlayerPlatformer snaps it to ledgePos1/ledgePos2 rather than moving it), so
/// there's nothing to space further stamps out along even if it kept trying.
///
/// Reuses FireTrailController/FireTrailCell completely unchanged — they only ever
/// deal in a world point + surface normal and don't know or care whether that surface
/// is a floor or a wall. Only the probe direction differs: instead of raycasting down
/// from the player's feet, this raycasts sideways (toward whichever side the player is
/// pressed against, from Flipped) from the player's side.
///
/// Runs its own, entirely independent controller pool — a wall-hang trail and a
/// ground trail can be burning simultaneously without interacting.
/// </summary>
public class FireTrailWallEmitter : FireTrailEmitterBase
{
    [Header("Wall Probe")]
    [SerializeField] private float _wallProbeDistance = 0.5f;
    [SerializeField] private float _wallProbeSkin = 0.05f;

    private bool _wasOnLedge;
    private float _lastSeenWallJumpTime;

    protected override void Awake()
    {
        base.Awake();

        // Seeded from the player's own current value rather than a guessed constant
        // (e.g. -1) — jumpVars.wallJumpedTime defaults to 0, and guessing wrong here
        // would read as "a wall jump just fired" on the very first Update() and place
        // a phantom stamp before the player has done anything at all.
        _lastSeenWallJumpTime = _playerCtrl.jumpVars.wallJumpedTime;
    }

    protected override Vector3 GetPoolParkPosition(int index)
    {
        // Offset from the ground emitter's parking spot so the two idle pools never
        // overlap in the Scene view.
        return new Vector3(1000f, -10000f - index * 5f, 0f);
    }

    private void Update()
    {
        // isWallHanging is only maintained by CheckSurroundings, which the
        // FixedUpdate switch only calls in the Platformer case — during OnLedge it's
        // simply left stale-true, so it can't be used on its own to detect the climb.
        // Gating everything through this explicit two-state check (rather than just
        // OR-ing isWallHanging in unconditionally) also keeps a stale-true flag from
        // lighting the trail up in states where it shouldn't, e.g. TakeDamage right
        // after an interrupted hang.
        bool isWallHanging = _playerCtrl.PlayerState == CTRL_PlayerPlatformer.playerControlState.Platformer
                           && _playerCtrl.groundCheck.isWallHanging;
        bool isOnLedge = _playerCtrl.PlayerState == CTRL_PlayerPlatformer.playerControlState.OnLedge;

        // A buffered jump lets executeJump() fire in the same FixedUpdate tick as the
        // CheckSurroundings call that just set isWallHanging true — applyMovement
        // (which contains the jump-buffer check) runs after CheckSurroundings in that
        // same method call, and a wall jump immediately clears isWallHanging again as
        // part of firing. Both the true and the false happen before this Update() ever
        // gets to look, so isWallHanging can go true-then-false entirely invisibly and
        // no run ever opens for a hang that genuinely happened. jumpVars.wallJumpedTime
        // updating is unambiguous proof a wall jump fired (only ever set from a wall
        // jump) regardless of whether isWallHanging was ever observed — if no run is
        // open when that happens, this hang was missed and gets its one stamp here.
        // Guarded on _currentController == null so an ordinary, already-observed hang
        // that ends in a normal wall jump doesn't get a redundant extra stamp.
        float wallJumpedTime = _playerCtrl.jumpVars.wallJumpedTime;
        bool missedBufferedWallJump = wallJumpedTime > _lastSeenWallJumpTime && _currentController == null;
        _lastSeenWallJumpTime = wallJumpedTime;

        if (!isWallHanging && !isOnLedge && !missedBufferedWallJump)
        {
            ReleaseCurrent();
            SetEmitting(false);
            return;
        }

        SetEmitting(true);

        if (isOnLedge)
        {
            // One-shot: exactly one stamp, right as the climb is initiated — not the
            // continuous spacing-gated stream TrySpawn does for an active wall-slide.
            if (!_wasOnLedge) PlaceLedgeClimbStamp();
        }
        else if (missedBufferedWallJump)
        {
            PlaceMissedWallJumpStamp();
        }
        else
        {
            TrySpawn();
        }

        _wasOnLedge = isOnLedge;
    }

    protected override void ReleaseCurrent()
    {
        _wasOnLedge = false;
        base.ReleaseCurrent();
    }

    // ── Spawning ─────────────────────────────────────────────────────────────
    private void TrySpawn()
    {
        // Wall the player is pressed against is whichever side they're facing —
        // matches CTRL_PlayerPlatformer's own wall-hang detection (wallDir there is
        // Vector2.right when !Flipped, Vector2.left when Flipped).
        Vector2 wallDir = _playerCtrl.Flipped ? Vector2.left : Vector2.right;

        // Cheap per-frame distance check (no raycast) — the AABB edge facing the wall,
        // at vertical center, is close enough to gate spawning; the actual stamp
        // position below is found with a proper wall probe.
        Vector2 cheapWallPos = GetCheapWallPos(wallDir);
        if (ShouldSkipSpacing(cheapWallPos)) return;

        PlaceStampAt(cheapWallPos, wallDir);
    }

    /// <summary>
    /// Places the single stamp that marks a ledge climb starting. Bypasses TrySpawn's
    /// spacing gate entirely — this fires exactly once, on the frame OnLedge begins.
    /// </summary>
    private void PlaceLedgeClimbStamp()
    {
        Vector2 wallDir = _playerCtrl.Flipped ? Vector2.left : Vector2.right;
        PlaceStampAt(GetCheapWallPos(wallDir), wallDir);
    }

    /// <summary>
    /// Places the stamp a wall jump earned when its hang was never actually observed
    /// (see the missedBufferedWallJump comment in Update). Flipped may already reflect
    /// the post-jump outward direction by the time this runs rather than the into-the-
    /// wall direction the hang itself had, so unlike the other placement paths this
    /// can't trust Flipped for which side to probe — it checks both.
    /// </summary>
    private void PlaceMissedWallJumpStamp()
    {
        TryFindWallEitherSide(out Vector2 point, out Vector2 normal);
        PlaceStampInto(point, normal);
    }

    private bool TryFindWallEitherSide(out Vector2 point, out Vector2 normal)
    {
        if (TryGetWallPoint(GetCheapWallPos(Vector2.right), Vector2.right, out point, out normal)) return true;
        if (TryGetWallPoint(GetCheapWallPos(Vector2.left), Vector2.left, out point, out normal)) return true;

        point = _playerCollider.bounds.center;
        normal = Vector2.up;
        return false;
    }

    private void PlaceStampAt(Vector2 wallPos, Vector2 wallDir)
    {
        TryGetWallPoint(wallPos, wallDir, out Vector2 point, out Vector2 normal);
        PlaceStampInto(point, normal);
    }

    private void PlaceStampInto(Vector2 point, Vector2 normal)
    {
        if (_currentController == null)
        {
            _currentController = GetPooledController();
            if (_currentController == null) return;   // maxPooledControllers < 1 misconfiguration guard
        }

        _currentController.PlaceStamp(point, normal);

        _lastSpawnPos = point;
        _hasLastSpawn = true;
    }

    private Vector2 GetCheapWallPos(Vector2 wallDir)
    {
        float x = wallDir.x > 0f ? _playerCollider.bounds.max.x : _playerCollider.bounds.min.x;
        return new Vector2(x, _playerCollider.bounds.center.y);
    }

    /// <summary>
    /// Finds the exact surface point/normal on the wall right now — this, not the
    /// player's transform, is what makes stamps sit correctly against the wall
    /// regardless of exactly how far the player's collider sits from it.
    /// </summary>
    private bool TryGetWallPoint(Vector2 footPos, Vector2 wallDir, out Vector2 point, out Vector2 normal)
    {
        Vector2 origin = footPos - wallDir * _wallProbeSkin;   // start just off the wall side, toward the player
        RaycastHit2D hit = Physics2D.Raycast(origin, wallDir, _wallProbeDistance + _wallProbeSkin, _groundMask);
        if (hit.collider != null)
        {
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        point = footPos;
        normal = -wallDir;
        return false;
    }
}
