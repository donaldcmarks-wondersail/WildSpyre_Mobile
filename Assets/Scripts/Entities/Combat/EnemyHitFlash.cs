using UnityEngine;

/// <summary>
/// Hit-flash: swaps every SpriteRenderer under the enemy to a flat-colour flash material for a few
/// frames, then restores the originals. Added and configured at runtime by CTRL_EnemyHealth from
/// SO_EnemyStats. Timed on Time.time, so during a hit-stop freeze the flash simply stays on and then
/// runs out its duration afterwards.
/// </summary>
public class EnemyHitFlash : MonoBehaviour
{
    private SpriteRenderer[] _renderers;
    private Material[][] _originalMaterials;
    private Material _flashMaterial;
    private float _duration;
    private float _flashUntil;
    private bool _flashing;

    public void Initialize(Material flashMaterial, float duration)
    {
        _flashMaterial = flashMaterial;
        _duration = duration;

        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _originalMaterials = new Material[_renderers.Length][];
        for (int i = 0; i < _renderers.Length; i++)
            _originalMaterials[i] = _renderers[i].sharedMaterials;
    }

    public void Play()
    {
        if (_flashMaterial == null || _renderers == null || _duration <= 0f) return;

        _flashUntil = Time.time + _duration;
        if (_flashing) return;

        _flashing = true;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;

            Material[] flash = new Material[_originalMaterials[i].Length];
            for (int m = 0; m < flash.Length; m++) flash[m] = _flashMaterial;
            _renderers[i].sharedMaterials = flash;
        }
    }

    private void Update()
    {
        if (_flashing && Time.time >= _flashUntil)
            Restore();
    }

    private void OnDisable()
    {
        if (_flashing) Restore();
    }

    private void Restore()
    {
        _flashing = false;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].sharedMaterials = _originalMaterials[i];
        }
    }
}
