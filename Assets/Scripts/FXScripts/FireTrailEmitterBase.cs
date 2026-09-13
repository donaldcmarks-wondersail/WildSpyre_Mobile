using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared machinery behind every fire-trail emitter (ground, wall, and any one-shot
/// source like a sling impact): the FireTrailController pool, the ember/spark
/// particle system, and the spacing state used to gate continuous stamp placement.
/// Field names are kept identical to what FireTrailEmitter/FireTrailWallEmitter
/// originally declared themselves, so this refactor doesn't reset any values already
/// wired on the scene's player instances — Unity matches serialized fields by name
/// across the whole inheritance chain, not just a class's own declarations.
///
/// What's NOT here, because it genuinely differs per surface type: the trigger
/// condition (Update), the spacing/probe logic (TrySpawn and friends), and how a
/// point + normal actually get found. Subclasses own all of that.
/// </summary>
public abstract class FireTrailEmitterBase : MonoBehaviour
{
    [Header("References")]
    [SerializeField] protected CTRL_PlayerPlatformer _playerCtrl;
    [SerializeField] protected CapsuleCollider2D _playerCollider;
    [Tooltip("FireTrailController prefab. Must be ACTIVE by default (unlike the cell prefab) so its own cell-pool builds at load time instead of hitching on first use.")]
    [SerializeField] protected FireTrailController _controllerPrefab;

    [Header("Spawn")]
    [Tooltip("World units the player must move before the next stamp.")]
    [SerializeField] protected float _spawnInterval = 0.35f;

    [Header("Surface Mask")]
    [Tooltip("Layers considered a valid surface when placing a stamp. Falls back to the player's own ground mask if left as Nothing.")]
    [SerializeField] protected LayerMask _groundMask;

    [Header("Controller Pool")]
    [Tooltip("How many separate trail runs can be alive/burning at once before the oldest is force-extinguished to make room.")]
    [SerializeField] protected int _maxPooledControllers = 4;

    [Header("Particles")]
    [Tooltip("Particle system for the trail's ember/spark effect. Auto-found among children if left unassigned.")]
    [SerializeField] protected ParticleSystem _particleSystem;
    [Tooltip("Emission rate (particles/sec) while actively laying down a trail. Forced to 0 the rest of the time.")]
    [SerializeField] protected float _maxEmission = 20f;

    protected readonly Queue<FireTrailController> _available = new Queue<FireTrailController>();
    protected readonly List<FireTrailController> _checkedOut = new List<FireTrailController>();

    protected FireTrailController _currentController;
    protected Vector2 _lastSpawnPos;
    protected bool _hasLastSpawn;

    protected virtual void Awake()
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
            Debug.LogWarning($"[{GetType().Name}] Max Pooled Controllers was {_maxPooledControllers} — clamping to 1.", this);
            _maxPooledControllers = 1;
        }

        for (int i = 0; i < _maxPooledControllers; i++)
        {
            FireTrailController controller = Instantiate(_controllerPrefab);
            controller.transform.position = GetPoolParkPosition(i);
            controller.gameObject.SetActive(false);
            _available.Enqueue(controller);
        }
    }

    protected virtual void Start()
    {
        if (_groundMask.value == 0)
            _groundMask = _playerCtrl.groundCheck.groundMask;
    }

    protected virtual void OnDisable()
    {
        ReleaseCurrent();
        SetEmitting(false);
    }

    /// <summary>
    /// Where an idle pooled controller sits before first use — parked well away from
    /// play so an idle pooled controller's own (inactive) cell pool doesn't visually
    /// sit on top of real level geometry. Overridden per emitter so separate emitters'
    /// pools never overlap each other in the Scene view either.
    /// </summary>
    protected virtual Vector3 GetPoolParkPosition(int index) => new Vector3(0f, -10000f - index * 5f, 0f);

    /// <summary>Forces the trail particle system's emission rate to Max Emission or 0. No-op if none is assigned.</summary>
    protected void SetEmitting(bool emitting)
    {
        if (_particleSystem == null) return;

        ParticleSystem.EmissionModule emission = _particleSystem.emission;
        emission.enabled = true;   // otherwise rateOverTime has no effect regardless of value
        emission.rateOverTime = emitting ? _maxEmission : 0f;
    }

    /// <summary>
    /// Ends whatever continuous run is in progress. Virtual so a subclass can reset
    /// extra state of its own (e.g. FireTrailWallEmitter's ledge-climb edge detector)
    /// alongside the shared cleanup.
    /// </summary>
    protected virtual void ReleaseCurrent()
    {
        if (_currentController == null) return;

        _currentController.Release();
        _currentController = null;
        _hasLastSpawn = false;   // the next continuous run places its first stamp immediately
    }

    /// <summary>True if the given position hasn't moved far enough from the last stamp yet to place another.</summary>
    protected bool ShouldSkipSpacing(Vector2 currentPos)
    {
        return _hasLastSpawn && Vector2.Distance(currentPos, _lastSpawnPos) < _spawnInterval;
    }

    /// <summary>
    /// Places a single stamp independent of any ongoing continuous run: checks out a
    /// controller from this emitter's own pool, places one stamp, and immediately
    /// releases it — the controller ages/renders/ticks damage on that one cell and
    /// self-returns to the pool once it burns out, exactly like a released continuous
    /// run winding down. Doesn't touch _currentController, so it can't interrupt or
    /// merge with whatever a continuous trail from this same emitter is doing. Used
    /// for one-off events, e.g. a sling bounce impact.
    /// </summary>
    public void PlaceOneShotStamp(Vector2 point, Vector2 normal)
    {
        FireTrailController controller = GetPooledController();
        if (controller == null) return;   // maxPooledControllers < 1 misconfiguration guard

        controller.PlaceStamp(point, normal);
        controller.Release();
    }

    // ── Controller pooling ───────────────────────────────────────────────────
    protected FireTrailController GetPooledController()
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
