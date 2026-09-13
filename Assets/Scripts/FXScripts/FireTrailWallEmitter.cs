using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wall-hang counterpart to FireTrailEmitter — same framework, different trigger and
/// probe direction. Lives on the player and decides WHEN and WHERE the wall fire trail
/// places a stamp: active for the entire duration of groundCheck.isWallHanging,
/// including the very first instant the player grabs the wall (before any downward
/// slide has actually started) — there's no minimum-speed gate here, unlike the
/// ground emitter, because "just grabbed, not sliding yet" is explicitly part of what
/// should show fire.
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
public class FireTrailWallEmitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CTRL_PlayerPlatformer _playerCtrl;
    [SerializeField] private CapsuleCollider2D _playerCollider;
    [Tooltip("FireTrailController prefab used for wall-hang trails (can be the same script as the ground trail's, on a separately-tuned prefab). Must be ACTIVE by default so its own cell-pool builds at load time instead of hitching on first use.")]
    [SerializeField] private FireTrailController _controllerPrefab;

    [Header("Spawn")]
    [Tooltip("World units the player must slide down the wall before the next stamp.")]
    [SerializeField] private float _spawnInterval = 0.25f;

    [Header("Wall Probe")]
    [Tooltip("Layers considered wall surface when placing a stamp. Falls back to the player's own ground mask if left as Nothing.")]
    [SerializeField] private LayerMask _groundMask;
    [SerializeField] private float _wallProbeDistance = 0.5f;
    [SerializeField] private float _wallProbeSkin = 0.05f;

    [Header("Controller Pool")]
    [Tooltip("How many separate wall-hang trail runs can be alive/burning at once before the oldest is force-extinguished to make room.")]
    [SerializeField] private int _maxPooledControllers = 4;

    [Header("Particles")]
    [Tooltip("Particle system for the wall trail's ember/spark effect. Auto-found among children if left unassigned.")]
    [SerializeField] private ParticleSystem _particleSystem;
    [Tooltip("Emission rate (particles/sec) while wall-hanging. Forced to 0 the rest of the time.")]
    [SerializeField] private float _maxEmission = 20f;

    private readonly Queue<FireTrailController> _available = new Queue<FireTrailController>();
    private readonly List<FireTrailController> _checkedOut = new List<FireTrailController>();

    private FireTrailController _currentController;
    private Vector2 _lastSpawnPos;
    private bool _hasLastSpawn;

    private void Awake()
    {
        if (_particleSystem == null) _particleSystem = GetComponentInChildren<ParticleSystem>(includeInactive: true);
        if (_particleSystem != null)
        {
            _particleSystem.Play();
            SetEmitting(false);
        }

        if (_maxPooledControllers < 1)
        {
            Debug.LogWarning($"[FireTrailWallEmitter] Max Pooled Controllers was {_maxPooledControllers} — clamping to 1.", this);
            _maxPooledControllers = 1;
        }

        for (int i = 0; i < _maxPooledControllers; i++)
        {
            FireTrailController controller = Instantiate(_controllerPrefab);
            // Parked well away from play so an idle pooled controller's own (inactive)
            // cell pool doesn't visually sit on top of real level geometry. Offset from
            // the ground emitter's parking spot too, so the two pools never overlap.
            controller.transform.position = new Vector3(1000f, -10000f - i * 5f, 0f);
            controller.gameObject.SetActive(false);
            _available.Enqueue(controller);
        }
    }

    private void Start()
    {
        if (_groundMask.value == 0)
            _groundMask = _playerCtrl.groundCheck.groundMask;
    }

    private void Update()
    {
        bool wallHanging = _playerCtrl.PlayerState == CTRL_PlayerPlatformer.playerControlState.Platformer
                         && _playerCtrl.groundCheck.isWallHanging;

        if (!wallHanging)
        {
            ReleaseCurrent();
            SetEmitting(false);
            return;
        }

        SetEmitting(true);
        TrySpawn();
    }

    private void OnDisable()
    {
        ReleaseCurrent();
        SetEmitting(false);
    }

    /// <summary>Forces the wall trail particle system's emission rate to Max Emission or 0. No-op if none is assigned.</summary>
    private void SetEmitting(bool emitting)
    {
        if (_particleSystem == null) return;

        ParticleSystem.EmissionModule emission = _particleSystem.emission;
        emission.enabled = true;   // otherwise rateOverTime has no effect regardless of value
        emission.rateOverTime = emitting ? _maxEmission : 0f;
    }

    private void ReleaseCurrent()
    {
        if (_currentController == null) return;

        _currentController.Release();
        _currentController = null;
        _hasLastSpawn = false;   // the next wall-hang event places its first stamp immediately
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
        if (_hasLastSpawn && Vector2.Distance(cheapWallPos, _lastSpawnPos) < _spawnInterval) return;

        if (_currentController == null)
        {
            _currentController = GetPooledController();
            if (_currentController == null) return;   // maxPooledControllers < 1 misconfiguration guard
        }

        TryGetWallPoint(cheapWallPos, wallDir, out Vector2 point, out Vector2 normal);
        _currentController.PlaceStamp(point, normal);

        _lastSpawnPos = cheapWallPos;
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

    // ── Controller pooling ───────────────────────────────────────────────────
    private FireTrailController GetPooledController()
    {
        FireTrailController controller;

        if (_available.Count > 0)
        {
            controller = _available.Dequeue();
        }
        else if (_checkedOut.Count > 0)
        {
            // Pool exhausted — extinguish the oldest still-burning run early rather
            // than growing the pool at runtime.
            controller = _checkedOut[0];
            _checkedOut.RemoveAt(0);
            controller.ForceFinish();
        }
        else
        {
            return null;   // maxPooledControllers < 1 misconfiguration guard
        }

        controller.Prepare(ReturnController);
        _checkedOut.Add(controller);
        return controller;
    }

    /// <summary>Called by a FireTrailController when it has fully burned out after being released.</summary>
    private void ReturnController(FireTrailController controller)
    {
        _checkedOut.Remove(controller);
        _available.Enqueue(controller);
    }
}
