using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Brief damage-dealing, damage-immune window fired the instant the sling actually launches the
/// player — on joystick release, not when aiming starts — plus the one-shot visual FX that sells
/// it, initially facing the real launch direction and then easing to track the player's live
/// movement direction while the sling continues (see TickRotationTracking). Fire() is called
/// directly by INPT_TouchManager.ExecuteSling() at the exact moment it applies launch force, the
/// same way INPT_AbilityJoystick calls straight into CTRL_PlayerAbilityController for taps/charges
/// — an event has an exact moment it happens, so it gets an exact call, not a polled state flag.
///
/// Both ongoing timers (the damage window and the rotation tracking) are plain Update() countdowns,
/// not coroutines: CTRL_AbilityController.CancelActiveAbilities taught us a coroutine can be stopped
/// mid-flight by something else entirely and silently skip its own cleanup (see Ability_Dash's
/// try/finally fix) — a countdown has no such failure mode, it just runs.
/// </summary>
public class CTRL_PlayerSlingImpact : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MNGR_PlayerLife _playerLife;
    [Tooltip("Read for the player's live velocity (the rotation-tracking target) and to know when " +
             "Sling has ended, so tracking stops rather than chasing a direction that no longer means " +
             "anything once the player's back on the ground.")]
    [SerializeField] private CTRL_PlayerPlatformer _playerCtrl;
    [Tooltip("Child hitbox with a PlayerAbilityDamager — its GameObject is switched on for Damage " +
             "Window Duration starting the instant the sling fires, same on/off-per-activation " +
             "pattern as the combo hitbox. Put it on the character art so it mirrors with Flipped, " +
             "same as PlayerAbilityDamager's other users.")]
    [SerializeField] private PlayerAbilityDamager _impactHitbox;
    [Tooltip("Visual-only art for the attack — enabled and rotated to face the launch direction the " +
             "instant the sling fires. Its own Particle System should have Stop Action set to " +
             "Disable so it switches itself back off when it finishes; this script only ever turns " +
             "it on, never off, and doesn't care whether it's still playing when the next window starts.")]
    [SerializeField] private GameObject _impactVisual;
    [Tooltip("Optional — explicitly Play()'d alongside enabling Impact Visual above, for a Particle " +
             "System with Play On Awake off (the normal setup for something triggered by gameplay " +
             "rather than firing the moment it's created). Leave empty if Play On Awake is on and " +
             "SetActive(true) alone is enough.")]
    [SerializeField] private ParticleSystem _impactParticles;
    [Tooltip("Sprite Renderer whose Color.a is driven by Window Alpha Curve over the damage window's " +
             "lifetime — e.g. a flash/glow on the hitbox or impact art. Independent of Impact Visual " +
             "above (that one's rotation-tracked and particle-driven; this one's a plain alpha tween). " +
             "Only alpha is touched — RGB is left exactly as authored on the sprite.")]
    [SerializeField] private SpriteRenderer _windowSpriteRenderer;
    [Tooltip("Maps elapsed/Damage Window Duration (0-1) to Color.a (0-1) for Window Sprite Renderer, " +
             "re-evaluated every frame the window is active. Default is a flash-then-fade: starts at " +
             "1, eases to 0 by the time the window ends.")]
    [SerializeField] private AnimationCurve _windowAlphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [Tooltip("Trail Renderer whose Color Gradient's alpha is multiplied by Trail Alpha Curve over the " +
             "damage window's lifetime — same idea as Window Alpha Curve above, but scaling the " +
             "gradient's existing alpha keys rather than overwriting a single Color.a. The gradient's " +
             "authored shape (where it's more/less transparent along the trail) is preserved; this " +
             "only ever multiplies it down, never changes its RGB or its relative alpha shape.")]
    [SerializeField] private TrailRenderer _windowTrailRenderer;
    [Tooltip("Maps elapsed/Damage Window Duration (0-1) to the alpha multiplier (0-1) applied to Window " +
             "Trail Renderer's gradient. Default: 1 at the start (gradient looks exactly as authored), " +
             "eased to 0 by the end (fully faded).")]
    [SerializeField] private AnimationCurve _trailAlphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [Tooltip("2D Point Light whose intensity is driven over the damage window's lifetime. Its resting " +
             "intensity is registered once in Awake() and used as the baseline — the final intensity " +
             "each frame is Base Intensity + (Light Intensity Curve(t) * Light Intensity Multiplier " +
             "Max), ADDED on top of the registered resting intensity, not a multiplier against it.")]
    [SerializeField] private Light2D _windowLight;
    [Tooltip("Scales Light Intensity Curve's output (typically authored 0-1) into the actual amount " +
             "ADDED on top of the registered base intensity — e.g. a curve peaking at 1 with this set " +
             "to 3 means the light gets up to 3 units brighter than its resting intensity at the peak.")]
    [SerializeField] private float _lightIntensityMultiplierMax = 2f;
    [Tooltip("Maps elapsed/Damage Window Duration (0-1) to a 0-1 factor (scaled by Light Intensity " +
             "Multiplier Max into the actual added amount), re-evaluated every frame the window is " +
             "active. Default: a quick flash up to full, fading to 0 by the time the window ends.")]
    [SerializeField] private AnimationCurve _lightIntensityCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Timing")]
    [Tooltip("Seconds after firing that the damage hitbox stays active and the player stays immune " +
             "to incoming damage.")]
    [SerializeField] private float _damageWindowDuration = 0.15f;

    [Header("Impact Visual Rotation")]
    [Tooltip("How long the impact visual takes to ease from facing the launch (aim) direction to " +
             "facing the player's live movement direction. Independent of Damage Window Duration " +
             "above — this can run longer (or shorter) than the hitbox stays active. After this " +
             "elapses it keeps tracking live movement direction for as long as the player's still " +
             "in Sling, since the target direction below is re-read every frame rather than fixed " +
             "at the start.")]
    [SerializeField] private float _rotationTrackDuration = 0.3f;
    [Tooltip("Maps elapsed/Rotation Track Duration (0-1) to the actual lerp factor (0-1) between the " +
             "launch direction and the live movement direction. Linear by default — ease the ends " +
             "for a slower start and/or a settle-in finish instead of a constant turn rate.")]
    [SerializeField] private AnimationCurve _rotationTrackCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Smooths the raw rb.linearVelocity-derived target direction before it's used as the lerp " +
             "target above. Without this, a player carrying strong pre-existing momentum into the " +
             "sling launch produces a resultant velocity (old momentum + new launch force, added — " +
             "not replaced) that can swing erratically for the first few physics steps before settling, " +
             "which was reading straight through into the visual as a sudden, sometimes near-180-degree " +
             "swing. 0 = no smoothing (raw velocity direction, the old behavior).")]
    [SerializeField] private float _targetSmoothTime = 0.08f;

    [Header("Hit")]
    [SerializeField] private int _damage = 1;
    [Tooltip("Hit-stop, camera shake and knockback/interrupt for this window's hits — carried by the " +
             "hitbox and read automatically by CTRL_EnemyHealth.TakeDamage, same as every other " +
             "player ability hit. Impact FX come from the hitbox's own Hit Particle Prefabs list.")]
    [SerializeField] private HitEffects _hitEffects = HitEffects.Default;

    private bool _windowActive;
    private float _windowTimer;

    private bool _rotationTrackingActive;
    private float _rotationTrackElapsed;
    private float _launchAngle;
    // AddForce() in ExecuteSling doesn't take effect until the next physics step — reading
    // rb.linearVelocity any earlier than that still reflects whatever pre-launch momentum the
    // player had (e.g. falling into the sling grab point), not the new launch direction. Gating
    // on this avoids ever blending toward that stale, occasionally near-opposite reading.
    private bool _physicsSettledSinceFire;
    private float _smoothedTargetAngle;
    private float _targetAngleSmoothVelocity;   // SmoothDampAngle's internal rate-of-change state

    // Trail gradient scratch state — captured once from whatever's authored on the Trail Renderer,
    // then every frame's scaled gradient is rebuilt FROM these originals (never from the previous
    // frame's already-scaled result), so repeated multiplication can't compound/decay wrongly, and
    // reused rather than reallocated each frame to avoid per-frame GC garbage.
    private GradientColorKey[] _trailBaseColorKeys;
    private GradientAlphaKey[] _trailBaseAlphaKeys;
    private GradientAlphaKey[] _trailScratchAlphaKeys;
    private Gradient _trailScratchGradient;

    // Registered once in Awake() from whatever's authored on the light — the multiplicative baseline
    // every frame's intensity is computed from, same "register the original, never mutate it in
    // place" principle as the trail gradient keys above.
    private float _lightBaseIntensity;

    private void Awake()
    {
        if (_impactHitbox != null)
        {
            _impactHitbox.damage = _damage;
            _impactHitbox.effects = _hitEffects;
            // Starts off — only Fire() turns it on.
            _impactHitbox.gameObject.SetActive(false);
        }

        if (_windowTrailRenderer != null)
        {
            Gradient authored = _windowTrailRenderer.colorGradient;
            _trailBaseColorKeys = authored.colorKeys;
            _trailBaseAlphaKeys = authored.alphaKeys;
            _trailScratchAlphaKeys = new GradientAlphaKey[_trailBaseAlphaKeys.Length];
            _trailScratchGradient = new Gradient();

            // Registered the authored gradient above; now start it off (multiplier 0) rather than
            // leaving whatever full-alpha gradient was authored in the Editor visible at rest —
            // only Fire() (and the active window) should ever bring it back up.
            ApplyTrailAlphaMultiplier(0f);
        }

        if (_windowLight != null)
            _lightBaseIntensity = _windowLight.intensity;
    }

    /// <summary>How far through the damage window we currently are: 0 the instant Fire() runs, 1 as
    /// it ends. Shared by every window-duration-driven visual (sprite alpha, trail alpha).</summary>
    private float WindowProgress() =>
        _damageWindowDuration > 0f ? Mathf.Clamp01(1f - _windowTimer / _damageWindowDuration) : 1f;

    private void Update()
    {
        if (_windowActive)
        {
            _windowTimer -= Time.deltaTime;
            ApplyWindowAlpha();
            ApplyTrailAlpha();
            ApplyLightIntensity();
            if (_windowTimer <= 0f)
                EndWindow();
        }

        if (_rotationTrackingActive)
            TickRotationTracking();
    }

    private void FixedUpdate()
    {
        _physicsSettledSinceFire = true;
    }

    /// <summary>Sets Window Sprite Renderer's alpha from Window Alpha Curve. RGB untouched.</summary>
    private void ApplyWindowAlpha()
    {
        if (_windowSpriteRenderer == null) return;

        Color c = _windowSpriteRenderer.color;
        c.a = Mathf.Clamp01(_windowAlphaCurve.Evaluate(WindowProgress()));
        _windowSpriteRenderer.color = c;
    }

    /// <summary>Evaluates Trail Alpha Curve at the current window progress and applies it.</summary>
    private void ApplyTrailAlpha()
    {
        if (_windowTrailRenderer == null || _trailBaseAlphaKeys == null) return;
        ApplyTrailAlphaMultiplier(Mathf.Clamp01(_trailAlphaCurve.Evaluate(WindowProgress())));
    }

    /// <summary>Multiplies Window Trail Renderer's gradient alpha keys by the given multiplier —
    /// rebuilt from the cached original keys every call, so the result tracks the multiplier exactly
    /// rather than compounding frame over frame. Color keys and the gradient's relative alpha shape
    /// (which keys are more/less transparent than others) are left exactly as authored. Also used
    /// directly by Awake() with multiplier 0 to start the trail off.</summary>
    private void ApplyTrailAlphaMultiplier(float multiplier)
    {
        if (_trailBaseAlphaKeys == null) return;

        for (int i = 0; i < _trailBaseAlphaKeys.Length; i++)
        {
            GradientAlphaKey key = _trailBaseAlphaKeys[i];
            key.alpha = Mathf.Clamp01(key.alpha * multiplier);
            _trailScratchAlphaKeys[i] = key;
        }

        _trailScratchGradient.SetKeys(_trailBaseColorKeys, _trailScratchAlphaKeys);
        _windowTrailRenderer.colorGradient = _trailScratchGradient;
    }

    /// <summary>Sets Window Light's intensity to Base Intensity + (Light Intensity Curve(progress) *
    /// Light Intensity Multiplier Max) — additive on top of the registered resting intensity, not a
    /// multiplier against it.</summary>
    private void ApplyLightIntensity()
    {
        if (_windowLight == null) return;

        float curveValue = Mathf.Max(0f, _lightIntensityCurve.Evaluate(WindowProgress()));
        _windowLight.intensity = _lightBaseIntensity + curveValue * _lightIntensityMultiplierMax;
    }

    /// <summary>Called by INPT_TouchManager.ExecuteSling() at the exact moment the sling actually
    /// launches the player (release, past the swipe threshold, with canSling true) — never on
    /// press/drag, and never on a release too small to launch. launchDirection is the real
    /// resulting travel direction (already negated from the pull-back aim direction by the
    /// caller), used to orient the impact visual.</summary>
    public void Fire(Vector2 launchDirection)
    {
        _windowActive = true;
        _windowTimer = _damageWindowDuration;
        // Apply t=0 immediately rather than waiting a frame for Update().
        ApplyWindowAlpha();
        ApplyTrailAlpha();
        ApplyLightIntensity();

        if (_impactHitbox != null)
            _impactHitbox.gameObject.SetActive(true);   // OnEnable() resets its per-activation hit tracking

        if (_impactVisual != null)
        {
            // World rotation, X axis toward launchDirection — same convention as
            // CTRL_EnemyProjectile.Launch (sprites drawn facing +X by default). Falls back to
            // whatever the visual's current rotation already is if launchDirection is ~zero
            // (shouldn't happen — ExecuteSling only calls Fire() past the swipe threshold).
            if (launchDirection.sqrMagnitude > 0.0001f)
            {
                _launchAngle = Mathf.Atan2(launchDirection.y, launchDirection.x) * Mathf.Rad2Deg;
                _impactVisual.transform.rotation = Quaternion.AngleAxis(_launchAngle, Vector3.forward);
            }

            // TEMP DIAGNOSTIC — remove once we know which side of the fence this is on.
            // Compares what we COMPUTED (launchDirection/_launchAngle) against what actually landed
            // on the transform (eulerAngles.z) right after setting it. If these two ever disagree,
            // the bug is upstream of this script (bad launchDirection, or something else touching
            // this transform the same frame). If they always agree and it still LOOKS wrong, the
            // transform itself is correct and the problem is in how the Particle System renders it
            // (Start Rotation randomization is the next thing to check on the asset itself).
            Debug.Log($"[SlingImpact] Fire() launchDirection={launchDirection} _launchAngle={_launchAngle:F1} " +
                      $"appliedEulerZ={_impactVisual.transform.eulerAngles.z:F1}", this);

            _impactVisual.SetActive(true);
            if (_impactParticles != null)
                _impactParticles.Play();

            _rotationTrackingActive = true;
            _rotationTrackElapsed = 0f;
            _physicsSettledSinceFire = false;   // don't trust rb.linearVelocity until FixedUpdate runs
            // Seed the smoothed target at the aim vector too (not wherever it last was, not a raw
            // velocity reading) — guarantees zero possibility of a jump at the start of the tween,
            // on top of the Lerp itself already pinning t=0 to _launchAngle.
            _smoothedTargetAngle = _launchAngle;
            _targetAngleSmoothVelocity = 0f;
        }

        // Player damage is explicitly excluded for this window — the sling bounce itself still
        // works, it's incoming DamagePlayer-tag hits that are blocked while this is on.
        _playerLife?.SetAbilityInvulnerable(true);
    }

    private void EndWindow()
    {
        _windowActive = false;

        if (_impactHitbox != null)
            _impactHitbox.gameObject.SetActive(false);

        _playerLife?.SetAbilityInvulnerable(false);
    }

    /// <summary>Eases the impact visual's facing from the launch direction toward the player's live
    /// movement direction. The target is re-read from the rigidbody every frame rather than fixed
    /// once at Fire() time, so after Rotation Track Duration elapses this keeps tracking wherever
    /// the sling's arc actually curves to, instead of freezing on a stale direction.</summary>
    private void TickRotationTracking()
    {
        bool stillSlinging = _playerCtrl != null
                           && _playerCtrl.PlayerState == CTRL_PlayerPlatformer.playerControlState.Sling;
        if (!stillSlinging)
        {
            _rotationTrackingActive = false;
            return;
        }

        if (_impactVisual == null || _playerCtrl.rb == null) return;

        // Hold at the launch angle (don't advance the timer either) until AddForce's result has
        // actually been integrated — see _physicsSettledSinceFire's comment for why this matters.
        if (!_physicsSettledSinceFire) return;

        Vector2 velocity = _playerCtrl.rb.linearVelocity;
        if (velocity.sqrMagnitude <= 0.0001f) return;   // momentarily stationary — hold last facing

        // Smoothed, not raw — see _targetSmoothTime's tooltip for why the raw per-frame reading
        // isn't trustworthy on its own right after launch.
        float rawTargetAngle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        _smoothedTargetAngle = _targetSmoothTime > 0f
            ? Mathf.SmoothDampAngle(_smoothedTargetAngle, rawTargetAngle, ref _targetAngleSmoothVelocity, _targetSmoothTime)
            : rawTargetAngle;

        _rotationTrackElapsed += Time.deltaTime;
        float t = _rotationTrackDuration > 0f ? Mathf.Clamp01(_rotationTrackElapsed / _rotationTrackDuration) : 1f;
        float curveT = Mathf.Clamp01(_rotationTrackCurve.Evaluate(t));

        float angle = Mathf.LerpAngle(_launchAngle, _smoothedTargetAngle, curveT);
        _impactVisual.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    private void OnDisable()
    {
        // Safety net if this component itself gets disabled mid-window — never leave the player
        // permanently invulnerable or the hitbox permanently live.
        if (_windowActive)
            EndWindow();
        _rotationTrackingActive = false;
    }
}
