using UnityEngine;

/// <summary>
/// Ground pound — execution lives here, all the tuning lives on SO_PlayerAbility_GroundPound (see
/// its own header for why they're split). Three entry points:
///
/// - NotifySlingLaunched(), called by INPT_TouchManager.ExecuteSling() at the moment a big flick
///   actually fires the sling. Captures THAT launch vector and checks it against Max Normal
///   Difference from straight down — this is the hard gate everything below sits behind. A sling
///   launched sideways (e.g. rolling across flat ground) can never qualify for ground pound no
///   matter how flat the ground it eventually lands on is — the earlier version of this script
///   only ever checked the ground's own flatness and never looked at how the player actually got
///   there, which is exactly why it fired on a flat-ground roll.
/// - OnCollisionEnter2D (this component's own, independent of CTRL_PlayerPlatformer's — Unity
///   calls every MonoBehaviour's collision callback on a GameObject, not just one) records whether
///   THE FIRST qualifying-layer contact since launch was also angle-qualifying, while Sling AND the
///   current sling passed the launch-direction gate above. Only ever the first contact — every
///   contact after it is ignored outright, win or lose, so a later bounce can never re-open or
///   extend the window a missed first one already closed.
/// - TryExecute(), called by INPT_TouchManager.ExecuteSling() at the exact moment a tap (not a
///   flick) would normally just drop the player back to Platformer control. Returns true if a
///   ground pound fired instead — the caller skips its own setStatePlatform() call in that case,
///   since Execute() below already puts the player in the (locked-input) GroundPound state itself.
///
/// Qualifying = the current sling's launch direction passed the gate above, AND the first ground
/// contact since launch (and only the first) was within Ground Pound Buffer seconds of the tap —
/// or, if no contact has happened yet, ground found right now within Max Distance From Ground.
/// Both the first contact and the immediate check are themselves checked against Max Normal
/// Difference so this can't fire on a wall, ceiling, or steep incline.
///
/// The pause-timer auto-return to Platformer is a plain Update() countdown, not a coroutine — same
/// reasoning as CTRL_PlayerSlingImpact: nothing here should depend on a coroutine surviving
/// something else stopping it mid-flight.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CTRL_PlayerGroundPound : MonoBehaviour
{
    [SerializeField] private SO_PlayerAbility_GroundPound _data;
    [SerializeField] private CTRL_PlayerPlatformer _playerCtrl;

    private bool _currentSlingEligible;      // did THIS sling's launch vector pass the down-check?
    // Only the FIRST ground contact after a launch is ever allowed to open the buffer window — set
    // true the instant any qualifying-layer contact is processed (whether or not it itself passed
    // the angle check), so a second/third/Nth bounce can never re-open or extend it. Without this,
    // every bounce refreshed _lastValidContactTime independently, so tapping shortly after ANY
    // bounce worked equally well — which is exactly why it only seemed to "work on the second
    // bounce": that was just whichever contact happened to land within the buffer of the tap, not
    // the first one specifically.
    private bool _firstContactConsumed;
    private float _lastValidContactTime = -999f;
    // The actual world position of whichever contact/raycast last qualified — ground projectiles
    // spawn from THIS, never from transform.position (the player's current position), which can be
    // mid-air by the time Execute() runs if the buffer window is what let it through.
    private Vector2 _lastContactPoint;
    private bool _pauseActive;
    private float _pauseTimer;

    private void Update()
    {
        if (!_pauseActive) return;

        _pauseTimer -= Time.deltaTime;
        if (_pauseTimer <= 0f)
        {
            _pauseActive = false;
            // Only hand control back if we're still the one holding it — something else (damage,
            // death) may have already moved the player out of GroundPound while we were counting
            // down, and this must never yank them back out of whatever that state is.
            if (_playerCtrl != null && _playerCtrl.PlayerState == CTRL_PlayerPlatformer.playerControlState.GroundPound)
                _playerCtrl.setStatePlatform();
        }
    }

    /// <summary>Called by INPT_TouchManager.ExecuteSling() the instant a big flick actually
    /// launches the player (same moment CTRL_PlayerSlingImpact.Fire() is called). launchDirection
    /// is the real resulting travel direction, already negated from the pull-back aim direction —
    /// this is the ONLY vector ground pound ever judges "was this aimed into the ground" against;
    /// the player's current/live velocity (which bouncing, rolling, or gravity can change) is never
    /// used for that judgment, only this one snapshot from the moment of launch.</summary>
    public void NotifySlingLaunched(Vector2 launchDirection)
    {
        _lastValidContactTime = -999f;   // a new sling invalidates any previous sling's buffered contact
        _firstContactConsumed = false;   // ...and gets its own fresh one-shot first-contact opportunity

        if (_data == null)
        {
            _currentSlingEligible = false;
            return;
        }

        float diff = NormalDifference(launchDirection, Vector2.down);
        _currentSlingEligible = diff <= _data.maxNormalDifference;

        Debug.Log($"[GroundPound] NotifySlingLaunched() launchDirection={launchDirection} " +
                  $"normalDifferenceFromDown={diff:F3} maxAllowed={_data.maxNormalDifference:F3} " +
                  $"eligible={_currentSlingEligible}", this);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_data == null || _playerCtrl == null) return;
        if (_playerCtrl.PlayerState != CTRL_PlayerPlatformer.playerControlState.Sling) return;
        if (!_currentSlingEligible) return;   // this sling wasn't launched steeply downward — never mind how flat the ground is
        if (_firstContactConsumed) return;    // the one-shot first-contact opportunity is already spent
        if (((1 << collision.gameObject.layer) & _data.groundMask.value) == 0) return;

        // A round/ball collider can register multiple contact points on one impact — take whichever
        // is closest to world-up (smallest difference) rather than just the first.
        float bestDifference = float.PositiveInfinity;
        Vector2 bestPoint = collision.GetContact(0).point;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            float diff = NormalDifference(contact.normal, Vector2.up);
            if (diff < bestDifference)
            {
                bestDifference = diff;
                bestPoint = contact.point;
            }
        }

        bool qualifies = bestDifference <= _data.maxNormalDifference;

        // Spent the instant a qualifying-layer contact is processed, win or lose — this is THE one
        // shot, not "keep checking until one qualifies."
        _firstContactConsumed = true;
        if (qualifies)
        {
            _lastValidContactTime = Time.time;
            _lastContactPoint = bestPoint;
        }

        // TEMP DIAGNOSTIC — remove once ground pound is confirmed working.
        Debug.Log($"[GroundPound] OnCollisionEnter2D (first contact) layer={LayerMask.LayerToName(collision.gameObject.layer)} " +
                  $"groundNormalDifference={bestDifference:F3} maxAllowed={_data.maxNormalDifference:F3} qualifies={qualifies}", this);
    }

    /// <summary>0 when a and b point the same way, growing to 1 at 90 degrees apart and 2 when
    /// opposite. 1 minus the dot product of the two normalized vectors — same style of check
    /// CTRL_PlayerPlatformer's own ground detection already uses via groundCheck.maxGroundedSlopeDot,
    /// just expressed as a difference instead of a raw dot product. Used both for "is the ground
    /// flat" (against Vector2.up) and "was the launch aimed downward" (against Vector2.down).</summary>
    private static float NormalDifference(Vector2 a, Vector2 b) => 1f - Vector2.Dot(a.normalized, b.normalized);

    /// <summary>Called by INPT_TouchManager.ExecuteSling() right where a tap-to-exit-Sling would
    /// otherwise just call setStatePlatform(). Returns true if ground pound fired.</summary>
    public bool TryExecute()
    {
        if (_data == null || _playerCtrl == null) return false;

        if (!_currentSlingEligible)
        {
            Debug.Log("[GroundPound] TryExecute() — current sling's launch direction never qualified, skipping entirely", this);
            return false;
        }

        bool bufferedContactValid = _firstContactConsumed
            && Time.time - _lastValidContactTime <= _data.groundPoundBuffer;
        // Only relevant before the first real contact has even been processed (the player taps the
        // literal instant they land, possibly the same frame OnCollisionEnter2D fires) — once
        // _firstContactConsumed is true, that one shot is already decided and this stops being
        // consulted, so a later proximity check can't re-open what the first contact already closed.
        bool immediateGroundValid = !_firstContactConsumed && IsGroundQualifyingRightNow();

        // TEMP DIAGNOSTIC — remove once ground pound is confirmed working.
        Debug.Log($"[GroundPound] TryExecute() bufferedContactValid={bufferedContactValid} " +
                  $"(age={Time.time - _lastValidContactTime:F2}s, buffer={_data.groundPoundBuffer:F2}s) " +
                  $"immediateGroundValid={immediateGroundValid}", this);

        if (!bufferedContactValid && !immediateGroundValid) return false;

        Execute();
        return true;
    }

    private bool IsGroundQualifyingRightNow()
    {
        if (_playerCtrl.rb == null) return false;

        RaycastHit2D hit = Physics2D.Raycast(_playerCtrl.rb.position, Vector2.down,
            _data.maxDistanceFromGround, _data.groundMask);
        if (hit.collider == null)
        {
            Debug.Log("[GroundPound] IsGroundQualifyingRightNow() — raycast found nothing within Max Distance From Ground", this);
            return false;
        }

        float diff = NormalDifference(hit.normal, Vector2.up);
        bool qualifies = diff <= _data.maxNormalDifference;
        if (qualifies)
            _lastContactPoint = hit.point;
        Debug.Log($"[GroundPound] IsGroundQualifyingRightNow() groundNormalDifference={diff:F3} " +
                  $"maxAllowed={_data.maxNormalDifference:F3} qualifies={qualifies}", this);
        return qualifies;
    }

    private void Execute()
    {
        _playerCtrl.setStateGroundPound();

        // The animation trigger and the ground projectiles fire back to back, right here, in the
        // same method call — there is no other code path that fires either one of these. If the
        // trigger fires, this line runs immediately after it, every time, no exceptions.
        if (!string.IsNullOrEmpty(_data.animTrigger))
            _playerCtrl.animatorChar?.SetTrigger(_data.animTrigger);
        SpawnProjectiles();

        HitStop.Trigger(_data.hitStopDuration);
        CameraShake.Shake(_data.shakeStrength, _data.shakeDuration);

        if (_data.impactParticlesPrefab != null)
            Instantiate(_data.impactParticlesPrefab, _lastContactPoint, Quaternion.identity);

        _currentSlingEligible = false;   // consumed — the next ground pound needs its own fresh sling launch
        _pauseActive = true;
        _pauseTimer = _data.groundPoundPauseTime;
    }

    /// <summary>Spawns both ground projectiles. Called from Execute() by default; wire this to an
    /// Animation Event on the ground pound clip instead (and remove the call in Execute()) if you
    /// want the spawn frame-locked to a specific point in the animation rather than instant.</summary>
    public void SpawnProjectiles()
    {
        SpawnGroundProjectile(Vector2.right);
        SpawnGroundProjectile(Vector2.left);
    }

    private void SpawnGroundProjectile(Vector2 direction)
    {
        if (_data.surfaceProjectilePrefab == null)
        {
            Debug.LogWarning("[GroundPound] SpawnGroundProjectile() — Surface Projectile Prefab is " +
                              "unassigned on the SO_PlayerAbility_GroundPound asset, nothing to spawn.", this);
            return;
        }

        // Spawn at the actual registered impact point, not transform.position (the player's current
        // position — can be mid-air by now if the buffer window is what let this through). Offset
        // up (clearance) so it's not embedded in the ground collider, and sideways in its own launch
        // direction (offset) so it's not spawning overlapping the player's own collider — that
        // overlap was exactly what was destroying these on their first physics step: the player
        // isn't tagged DamagePlayer/Enemy or on the projectile's ground mask, so
        // CTRL_SurfaceProjectile.OnTriggerEnter2D was treating the player as "hit something, die."
        Vector2 spawnPos = _lastContactPoint
            + Vector2.up * _data.projectileSpawnClearance
            + direction * _data.projectileSpawnOffset;

        GameObject proj = Instantiate(_data.surfaceProjectilePrefab, spawnPos, Quaternion.identity);
        IThrownProjectile thrown = proj.GetComponent<IThrownProjectile>();

        if (thrown == null)
        {
            Debug.LogWarning($"[GroundPound] SpawnGroundProjectile() — '{_data.surfaceProjectilePrefab.name}' " +
                              "has no IThrownProjectile component (e.g. CTRL_SurfaceProjectile), so it was " +
                              "instantiated but never launched — it'll just sit at the impact point.", proj);
            return;
        }

        thrown.Launch(direction, _data.projectileLaunchSpeed);
        Debug.Log($"[GroundPound] SpawnGroundProjectile() launched '{proj.name}' direction={direction} " +
                  $"speed={_data.projectileLaunchSpeed} at {spawnPos} (impact point was {_lastContactPoint})", proj);
    }

    private void OnDisable()
    {
        // Safety net if this component gets disabled mid-pause — never leave the player
        // permanently input-locked in GroundPound.
        if (_pauseActive && _playerCtrl != null
            && _playerCtrl.PlayerState == CTRL_PlayerPlatformer.playerControlState.GroundPound)
            _playerCtrl.setStatePlatform();
        _pauseActive = false;
    }
}
