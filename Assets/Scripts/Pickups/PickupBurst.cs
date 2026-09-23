using UnityEngine;

/// <summary>
/// Optional "intro" component, same pattern as PickupMagnetize: while active it takes over the
/// transform, and Pickup/PickupMagnetize both defer to it (see their IsBursting checks) so
/// nothing fights over position mid-burst. Not triggered automatically — whoever spawns the
/// pickup (e.g. CTRL_EnemyPickupDrop) calls Begin() right after MNGR_PickupManager.Spawn() to
/// kick it off. A pickup with no PickupBurst, or one that's never had Begin() called, just skips
/// straight to its normal idle IPickupMotion — this only matters for burst-spawned drops.
///
/// Self-driven (its own Update(), not ticked by Pickup) for the same reason PickupMagnetize is:
/// it needs to keep running the one frame Pickup itself might be mid-pool-reuse-setup.
///
/// Arc math is a plain 0-4t(1-t) parabola (peaks at the midpoint, zero at both ends) added on top
/// of a straight lerp from start to landing — good enough for a half-second toss with no physics
/// engine involved, consistent with the rest of the pickup system staying physics-free.
/// </summary>
public class PickupBurst : MonoBehaviour
{
    private enum State { Idle, Arcing, Bouncing, Done }

    [Header("Arc")]
    [Tooltip("Horizontal distance from the spawn point, randomized per pickup so a cluster fans out.")]
    [SerializeField] private float _minDistance = 0.5f;
    [SerializeField] private float _maxDistance = 1.5f;
    [SerializeField] private float _minApexHeight = 0.75f;
    [SerializeField] private float _maxApexHeight = 1.5f;
    [SerializeField] private float _arcDuration = 0.35f;

    [Header("Landing")]
    [Tooltip("Layer the downward raycast checks to find the real floor height under the burst target " +
             "— keeps drops from floating/sinking on stairs or ledges instead of the flat spawn height.")]
    [SerializeField] private LayerMask _groundLayerMask;
    [Tooltip("Max step down (or up) from the enemy's own footing that a burst target's floor is " +
             "allowed to be. This isn't a post-hoc filter — it's literally how far each raycast is " +
             "allowed to search, anchored at the enemy's own floor height each time, so an unrelated " +
             "platform or a pit outside this range is never reachable by the ray in the first place.")]
    [SerializeField] private float _maxGroundStep = 1f;
    [Tooltip("How far above the raycast-hit floor the pickup actually rests, so its pivot/collider " +
             "doesn't sit embedded in the ground. Tune per pickup sprite if it still looks sunken or floaty.")]
    [SerializeField] private float _groundClearance = 0.1f;

    [Header("Bounce Settle")]
    [SerializeField] private int _bounceCount = 2;
    [Tooltip("First bounce's height. Each following bounce is this times Bounce Decay, again and again.")]
    [SerializeField] private float _firstBounceHeight = 0.2f;
    [SerializeField, Range(0f, 1f)] private float _bounceDecay = 0.45f;
    [SerializeField] private float _bounceDuration = 0.16f;

    private State _state = State.Idle;
    private float _elapsed;
    private Vector2 _startPos;
    private Vector2 _landingPos;
    private float _apexHeight;
    private int _bouncesLeft;
    private float _bounceHeight;
    private Vector2 _bounceFrom;

    public bool IsBursting { get; private set; }

    /// <summary>Kicks off the arc from this GameObject's current position. hitDirection is
    /// typically CTRL_EnemyHealth.LastHitDirection — only its horizontal sign is used (this is a
    /// 2D platformer; verticality comes from the arc itself, not from the hit direction).</summary>
    public void Begin(Vector2 hitDirection)
    {
        _startPos = transform.position;
        _elapsed = 0f;

        float lean = Mathf.Abs(hitDirection.x) > 0.01f ? Mathf.Sign(hitDirection.x) : (Random.value < 0.5f ? -1f : 1f);
        float horizontalOffset = lean * Random.Range(_minDistance, _maxDistance);
        _apexHeight = Random.Range(_minApexHeight, _maxApexHeight);

        Vector2 targetXY = _startPos + new Vector2(horizontalOffset, 0f);
        _landingPos = ResolveGroundHeight(targetXY);

        _bouncesLeft = _bounceCount;
        _bounceHeight = _firstBounceHeight;

        _state = State.Arcing;
        IsBursting = true;
    }

    private Vector2 ResolveGroundHeight(Vector2 targetXY)
    {
        // Anchor everything to the enemy's own footing, found with a short ray right around where
        // it's already standing — no need to search far since it's on solid ground at the moment
        // it bursts. Every other search below is then re-anchored at THAT height, not restarted
        // from transform.position, so an unrelated platform or a pit can never be reached even by
        // a lucky raycast — it's outside the search band by construction, not filtered out after
        // the fact (the earlier version's bug: a wide up-and-down search plus an after-the-fact
        // distance check, which broke down whenever the wrong platform was within reach of BOTH
        // the reference check and the target check — they'd agree with each other and pass).
        bool haveOwnGround = TryFindGroundY(_startPos.x, _startPos.y, out float ownGroundY);
        float refY = haveOwnGround ? ownGroundY : _startPos.y;

        if (TryFindGroundY(targetXY.x, refY, out float groundY))
            return new Vector2(targetXY.x, groundY + _groundClearance);

        // Nothing within Max Ground Step of the enemy's own floor under the offset target (a ledge,
        // a pit) — land back at the enemy's own X instead of committing to open air.
        return new Vector2(_startPos.x, refY + _groundClearance);
    }

    /// <summary>Searches for ground within [aroundY - MaxGroundStep, aroundY + a small clearance
    /// band] at the given X — never further, so a platform well above or below aroundY is
    /// structurally unreachable rather than merely unlikely.</summary>
    private bool TryFindGroundY(float x, float aroundY, out float groundY)
    {
        const float rayStartClearance = 0.25f; // just enough the ray doesn't start inside the floor itself
        Vector2 rayStart = new Vector2(x, aroundY + rayStartClearance);
        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, rayStartClearance + _maxGroundStep, _groundLayerMask);
        if (hit.collider != null)
        {
            groundY = hit.point.y;
            return true;
        }

        groundY = 0f;
        return false;
    }

    private void OnDisable()
    {
        // Pooled reuse — make sure the next Begin() starts clean rather than resuming mid-bounce.
        _state = State.Idle;
        IsBursting = false;
    }

    private void Update()
    {
        if (_state == State.Idle || _state == State.Done) return;

        _elapsed += Time.deltaTime;

        if (_state == State.Arcing)
        {
            float t = Mathf.Clamp01(_elapsed / _arcDuration);
            Vector2 flat = Vector2.Lerp(_startPos, _landingPos, t);
            float hump = 4f * _apexHeight * t * (1f - t);
            transform.position = new Vector3(flat.x, flat.y + hump, transform.position.z);

            if (t >= 1f)
            {
                transform.position = _landingPos;
                BeginBounceOrFinish(_landingPos);
            }
            return;
        }

        // Bouncing
        {
            float t = Mathf.Clamp01(_elapsed / _bounceDuration);
            float hump = 4f * _bounceHeight * t * (1f - t);
            transform.position = new Vector3(_bounceFrom.x, _bounceFrom.y + hump, transform.position.z);

            if (t >= 1f)
            {
                transform.position = _bounceFrom;
                _bounceHeight *= _bounceDecay;
                BeginBounceOrFinish(_bounceFrom);
            }
        }
    }

    private void BeginBounceOrFinish(Vector2 from)
    {
        if (_bouncesLeft > 0)
        {
            _bouncesLeft--;
            _elapsed = 0f;
            _bounceFrom = from;
            _state = State.Bouncing;
        }
        else
        {
            _state = State.Done;
            IsBursting = false;
        }
    }
}
