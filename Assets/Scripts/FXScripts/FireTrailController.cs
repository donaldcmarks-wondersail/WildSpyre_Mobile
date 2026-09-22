using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One independent, pooled "trail run" — its own LineRenderer, its own chain of
/// FireTrailCell hitboxes, its own damage-tick bookkeeping. Completely self-contained
/// and NOT parented to the player (or anything else that moves) — that's what lets
/// separate platforms get separate, disconnected trails instead of one continuous
/// ribbon bridging every gap the player has ever jumped.
///
/// FireTrailEmitter checks one of these out of a pool on each new grounding event and
/// feeds it stamps via PlaceStamp() while the player keeps moving on that patch of
/// ground. Once Release()d (the player left the ground) it keeps rendering/aging/
/// ticking damage exactly as before, and once every one of its cells has individually
/// burned out it returns itself to the pool automatically.
/// </summary>
public class FireTrailController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LineRenderer _lineRenderer;
    [SerializeField] private FireTrailCell _cellPrefab;

    [Header("Stamp")]
    [Tooltip("Half-width of each stamp's box trigger (the box height and offset come from the cell prefab). Should be a little more than half of the emitter's Spawn Interval so neighbors just overlap with no gaps.")]
    [SerializeField] private float _cellRadius = 0.2f;
    [Tooltip("Pool size / max simultaneous stamps in THIS run. Also caps the run's visible length.")]
    [SerializeField] private int _maxActiveCells = 24;
    [Tooltip("How far above the surface (along its normal) the visual ribbon sits, so it doesn't z-fight/clip into sloped ground.")]
    [SerializeField] private float _visualSurfaceOffset = 0.03f;

    [Header("Burn / Damage")]
    [SerializeField] private float _burnLifetime = 2.5f;
    [SerializeField] private float _tickInterval = 0.5f;
    [SerializeField] private int _tickDamage = 1;

    [Header("Light")]
    [Tooltip("0-1 multiplier against each cell's own authored point-light intensity, evaluated over its normalized age (0 = just placed, 1 = about to expire). Cells with no light assigned simply ignore this.")]
    [SerializeField] private AnimationCurve _lightIntensityCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [Tooltip("Light color at spawn (age 0).")]
    [SerializeField] private Color _lightStartColor = new Color(1f, 0.95f, 0.6f);
    [Tooltip("Light color at expiry (age 1) — lerped from Light Start Color over the burn.")]
    [SerializeField] private Color _lightEndColor = new Color(0.6f, 0.05f, 0f);

    private readonly Queue<FireTrailCell> _pool = new Queue<FireTrailCell>();
    private readonly List<FireTrailCell> _active = new List<FireTrailCell>();   // oldest first

    private readonly Dictionary<IFireDamageable, int> _overlapCounts = new Dictionary<IFireDamageable, int>();
    private readonly Dictionary<IFireDamageable, float> _nextTick = new Dictionary<IFireDamageable, float>();
    private readonly List<IFireDamageable> _tickBuffer = new List<IFireDamageable>();

    private Transform _cellPoolRoot;
    private Action<FireTrailController> _returnToPool;
    private bool _released;

    private void Awake()
    {
        if (_lineRenderer == null) _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;   // required — RebuildRibbon writes world-space positions

        // This controller's own transform never moves once created, so parenting the
        // cell pool under it is safe (unlike the player-child setup this replaces).
        _cellPoolRoot = new GameObject("CellPool").transform;
        _cellPoolRoot.SetParent(transform, false);

        if (_maxActiveCells < 1)
        {
            Debug.LogWarning($"[FireTrailController] Max Active Cells was {_maxActiveCells} — clamping to 1.", this);
            _maxActiveCells = 1;
        }

        for (int i = 0; i < _maxActiveCells; i++)
        {
            FireTrailCell cell = Instantiate(_cellPrefab, _cellPoolRoot);
            cell.gameObject.SetActive(false);
            _pool.Enqueue(cell);
        }
    }

    /// <summary>
    /// Checks this controller out of the pool for a new run. Called by FireTrailEmitter
    /// on the first stamp of a fresh grounding event.
    /// </summary>
    public void Prepare(Action<FireTrailController> returnToPool)
    {
        _returnToPool = returnToPool;
        _released = false;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Marks this run as finished accepting new stamps (the player left the ground).
    /// It keeps rendering/aging/ticking damage normally until every cell has
    /// individually burned out, then returns itself to the pool on its own.
    /// </summary>
    public void Release()
    {
        _released = true;
    }

    /// <summary>
    /// Immediately extinguishes this run — every cell returned to this controller's
    /// own pool right now, regardless of remaining lifetime — and clears its ribbon.
    /// Used only when the emitter's controller pool is exhausted and a new grounding
    /// event needs one anyway.
    /// </summary>
    public void ForceFinish()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            _active[i].Deactivate();
            _pool.Enqueue(_active[i]);
        }
        _active.Clear();
        _overlapCounts.Clear();
        _nextTick.Clear();
        _lineRenderer.positionCount = 0;
    }

    /// <summary>
    /// Places one stamp at an already-resolved ground point. Called by FireTrailEmitter.
    /// alignToNormal rotates the stamp's fire to burn along the surface normal (see FireTrailCell.Activate).
    /// </summary>
    public void PlaceStamp(Vector2 point, Vector2 normal, bool alignToNormal = false)
    {
        FireTrailCell cell = GetPooledCell();
        if (cell == null) return;   // maxActiveCells < 1 misconfiguration — skip rather than throw

        cell.Activate(point, normal, _cellRadius, this, alignToNormal);
        _active.Add(cell);
    }

    private void Update()
    {
        ExpireOldCells();
        UpdateVisuals();
        TickDamage();

        if (_released && _active.Count == 0)
        {
            gameObject.SetActive(false);
            Action<FireTrailController> callback = _returnToPool;
            _returnToPool = null;
            callback?.Invoke(this);
        }
    }

    // ── Cell pooling ─────────────────────────────────────────────────────────
    private FireTrailCell GetPooledCell()
    {
        if (_pool.Count > 0) return _pool.Dequeue();

        // Pool exhausted at maxActiveCells — recycle the oldest stamp early instead of
        // growing the pool at runtime. Both collections empty here should be
        // impossible post-Awake (see the clamp above), but fail safe rather than
        // throw if some future change breaks that invariant.
        if (_active.Count == 0) return null;

        FireTrailCell oldest = _active[0];
        _active.RemoveAt(0);
        oldest.Deactivate();
        return oldest;
    }

    private void ExpireOldCells()
    {
        while (_active.Count > 0 && Time.time - _active[0].SpawnTime >= _burnLifetime)
        {
            FireTrailCell cell = _active[0];
            _active.RemoveAt(0);
            cell.Deactivate();
            _pool.Enqueue(cell);
        }
    }

    // ── Visual ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Rewrites the ribbon from the live stamp list and drives each cell's optional
    /// point light. Index 0 is always the oldest (about-to-expire) stamp and the last
    /// index the newest, so a widthCurve/colorGradient authored on the LineRenderer
    /// (evaluated along its length) already fades the tail out for free — no
    /// per-vertex curve sampling needed for the ribbon. The light, unlike the
    /// LineRenderer, has no such built-in per-length evaluation, so its intensity
    /// multiplier (against its own authored base intensity — see FireTrailCell) and
    /// its color (lerped between Light Start/End Color) are both computed explicitly
    /// here from each cell's own normalized age.
    /// </summary>
    private void UpdateVisuals()
    {
        _lineRenderer.positionCount = _active.Count;
        for (int i = 0; i < _active.Count; i++)
        {
            FireTrailCell cell = _active[i];

            Vector2 p = (Vector2)cell.transform.position + cell.SurfaceNormal * _visualSurfaceOffset;
            _lineRenderer.SetPosition(i, p);

            float age01 = Mathf.Clamp01((Time.time - cell.SpawnTime) / _burnLifetime);
            Color color = Color.Lerp(_lightStartColor, _lightEndColor, age01);
            cell.UpdateLight(_lightIntensityCurve.Evaluate(age01), color);
        }
    }

    // ── Damage ───────────────────────────────────────────────────────────────
    /// <summary>Called by a FireTrailCell when a new target enters it.</summary>
    public void RegisterOverlap(IFireDamageable target)
    {
        if (_overlapCounts.TryGetValue(target, out int count))
        {
            _overlapCounts[target] = count + 1;
            return;
        }

        _overlapCounts[target] = 1;
        _nextTick[target] = Time.time;   // first tick lands immediately on entry
    }

    /// <summary>Called by a FireTrailCell when a target leaves it (or the cell is deactivated).</summary>
    public void UnregisterOverlap(IFireDamageable target)
    {
        if (!_overlapCounts.TryGetValue(target, out int count)) return;

        if (count <= 1)
        {
            _overlapCounts.Remove(target);
            _nextTick.Remove(target);
        }
        else
        {
            _overlapCounts[target] = count - 1;
        }
    }

    private void TickDamage()
    {
        if (_overlapCounts.Count == 0) return;

        // Snapshot the keys — ApplyBurnTick can kill a target and unwind back into
        // Unregister via its own cleanup, which would mutate _overlapCounts mid-iteration.
        _tickBuffer.Clear();
        _tickBuffer.AddRange(_overlapCounts.Keys);

        foreach (IFireDamageable target in _tickBuffer)
        {
            // A target held only through this interface reference doesn't benefit from
            // Unity's fake-null check unless we ask for it explicitly like this — a
            // destroyed MonoBehaviour here would otherwise throw on the call below.
            if (target is UnityEngine.Object unityObj && unityObj == null)
            {
                _overlapCounts.Remove(target);
                _nextTick.Remove(target);
                continue;
            }

            if (!_overlapCounts.ContainsKey(target)) continue;
            if (!_nextTick.TryGetValue(target, out float t) || Time.time < t) continue;

            target.ApplyBurnTick(_tickDamage);
            _nextTick[target] = Time.time + _tickInterval;
        }
    }
}
