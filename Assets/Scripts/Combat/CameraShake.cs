using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Camera shake as a Cinemachine extension: offsets the final camera position after the follow/damping
/// pipeline, so it never disturbs how the camera tracks the player. Added at runtime to the live
/// Cinemachine virtual camera on the first Shake call — nothing to place or wire in the scene.
///
/// Runs on unscaled time so the shake keeps playing through a hit-stop freeze.
/// </summary>
public class CameraShake : CinemachineExtension
{
    private const float NoiseFrequency = 35f;

    private static CameraShake _instance;

    private float _strength;
    private float _startTime;
    private float _duration;
    private float _seedX;
    private float _seedY;

    protected override void Awake()
    {
        base.Awake();
        _seedX = Random.value * 100f;   // not in a field initializer — Unity forbids Random there
        _seedY = Random.value * 100f;
    }

    /// <summary>Shake the camera. A stronger call replaces a weaker one in progress; a weaker one is ignored.</summary>
    public static void Shake(float strength, float duration)
    {
        if (strength <= 0f || duration <= 0f) return;

        if (_instance == null)
        {
            // The camera the Brain is actually showing; falls back to any virtual camera in the scene.
            // Typed as the shared base class on purpose: the scene's CM_vCam_Player is the older
            // CinemachineVirtualCamera, which is NOT a CinemachineCamera.
            CinemachineBrain brain = CinemachineBrain.GetActiveBrain(0);
            CinemachineVirtualCameraBase vcam = brain != null
                ? brain.ActiveVirtualCamera as CinemachineVirtualCameraBase
                : null;
            if (vcam == null) vcam = Object.FindFirstObjectByType<CinemachineVirtualCameraBase>();
            if (vcam == null) return;

            _instance = vcam.GetComponent<CameraShake>();
            if (_instance == null) _instance = vcam.gameObject.AddComponent<CameraShake>();
        }

        _instance.Add(strength, duration);
    }

    private float CurrentAmplitude()
    {
        if (_duration <= 0f) return 0f;
        float remaining01 = 1f - (Time.unscaledTime - _startTime) / _duration;
        return remaining01 > 0f ? _strength * remaining01 * remaining01 : 0f;
    }

    private void Add(float strength, float duration)
    {
        if (strength < CurrentAmplitude()) return;

        _strength = strength;
        _duration = duration;
        _startTime = Time.unscaledTime;
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize) return;

        float amplitude = CurrentAmplitude();
        if (amplitude <= 0f) return;

        float t = Time.unscaledTime * NoiseFrequency;
        float x = (Mathf.PerlinNoise(t, _seedX) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(t, _seedY) - 0.5f) * 2f;

        state.PositionCorrection += new Vector3(x, y, 0f) * amplitude;
    }
}
