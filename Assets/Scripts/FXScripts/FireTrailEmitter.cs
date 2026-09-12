using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lives on the player and decides WHEN and WHERE the ground fire trail places a
/// stamp — grounded/moving gating, spacing, and the ground probe that makes stamps
/// conform to slopes. Owns no visuals or colliders itself; instead it checks a
/// FireTrailController out of a pool on each new grounding event and hands it
/// resolved world points via PlaceStamp().
///
/// Controllers are standalone GameObjects, NOT parented to the player, so a placed
/// stamp never gets dragged along afterward — and because leaving the ground clears
/// the "current" controller (the next one is a fresh pool checkout), dropping to a
/// different platform starts a brand new, disconnected trail instead of bridging back
/// to wherever the last one left off.
/// </summary>
public class FireTrailEmitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CTRL_PlayerPlatformer _playerCtrl;
    [SerializeField] private CapsuleCollider2D _playerCollider;
    [Tooltip("FireTrailController prefab. Must be ACTIVE by default (unlike the cell prefab) so its own cell-pool builds at load time instead of hitching on first use.")]
    [SerializeField] private FireTrailController _controllerPrefab;

    [Header("Spawn")]
    [Tooltip("World units the player must move (while grounded, in Platformer mode) before the next stamp.")]
    [SerializeField] private float _spawnInterval = 0.35f;
    [Tooltip("Below this speed, standing still leaves no new stamps.")]
    [SerializeField] private float _minSpeedToTrail = 0.5f;

    [Header("Ground Probe")]
    [Tooltip("Layers considered ground when placing a stamp. Falls back to the player's own ground mask if left as Nothing.")]
    [SerializeField] private LayerMask _groundMask;
    [SerializeField] private float _groundProbeDistance = 0.5f;
    [SerializeField] private float _groundProbeSkin = 0.05f;

    [Header("Controller Pool")]
    [Tooltip("How many separate trail runs (e.g. across different platforms) can be alive/burning at once before the oldest is force-extinguished to make room.")]
    [SerializeField] private int _maxPooledControllers = 4;

    [Header("Particles")]
    [Tooltip("Particle system for the trail's ember/spark effect. Auto-found among children if left unassigned.")]
    [SerializeField] private ParticleSystem _particleSystem;
    [Tooltip("Emission rate (particles/sec) while actively laying down a trail (grounded, in Platformer mode, at or above Min Speed To Trail). Forced to 0 the rest of the time.")]
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
            // Make sure it's actually simulating regardless of its own Play On Awake
            // setting — emission rate has no effect on a stopped system.
            _particleSystem.Play();
            SetEmitting(false);
        }

        if (_maxPooledControllers < 1)
        {
            Debug.LogWarning($"[FireTrailEmitter] Max Pooled Controllers was {_maxPooledControllers} — clamping to 1.", this);
            _maxPooledControllers = 1;
        }

        for (int i = 0; i < _maxPooledControllers; i++)
        {
            FireTrailController controller = Instantiate(_controllerPrefab);
            // Parked well away from play so an idle pooled controller's own (inactive)
            // cell pool doesn't visually sit on top of real level geometry — same
            // reasoning as the old cell-pool parking, just one level up now.
            controller.transform.position = new Vector3(0f, -10000f - i * 5f, 0f);
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
        bool isEmittingTrail = _playerCtrl.rb.linearVelocity.magnitude >= _minSpeedToTrail;
        SetEmitting(isEmittingTrail);

        if (isEmittingTrail) TrySpawn();
    }

    private void OnDisable()
    {
        ReleaseCurrent();
        SetEmitting(false);
    }

    /// <summary>Forces the trail particle system's emission rate to Max Emission or 0. No-op if none is assigned.</summary>
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
        _hasLastSpawn = false;   // next grounding event places its first stamp immediately
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
        if (_hasLastSpawn && Vector2.Distance(cheapFootPos, _lastSpawnPos) < _spawnInterval) return;

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
