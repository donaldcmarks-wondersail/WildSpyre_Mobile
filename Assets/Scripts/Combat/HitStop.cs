using System.Collections;
using UnityEngine;

/// <summary>
/// Brief whole-game freeze on impact ("hit-stop"). Sets Time.timeScale to 0 and restores it after a
/// real-time (unscaled) delay — a scaled timer would never finish while frozen.
///
/// Never fights another owner of the time scale: it won't start while the scale isn't 1 (the player's
/// sling slow-motion), and if something else changes the scale mid-freeze it steps aside without
/// writing to it. Overlapping calls extend the freeze to the longest, they don't stack.
/// </summary>
public static class HitStop
{
    private static bool _active;
    private static float _endUnscaledTime;
    private static HitStopRunner _runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _active = false;
        _runner = null;
    }

    public static void Trigger(float duration)
    {
        if (duration <= 0f) return;

        if (_active)
        {
            _endUnscaledTime = Mathf.Max(_endUnscaledTime, Time.unscaledTime + duration);
            return;
        }

        // Someone else owns the time scale (slow-mo, pause) — leave it alone.
        if (!Mathf.Approximately(Time.timeScale, 1f)) return;

        if (_runner == null)
        {
            GameObject go = new GameObject("HitStopRunner") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<HitStopRunner>();
        }

        _active = true;
        _endUnscaledTime = Time.unscaledTime + duration;
        Time.timeScale = 0f;
        _runner.StartCoroutine(FreezeCO());
    }

    private static IEnumerator FreezeCO()
    {
        while (Time.unscaledTime < _endUnscaledTime && Time.timeScale == 0f)
            yield return null;

        // Only put the scale back if it's still the freeze value — otherwise another system took over.
        if (Time.timeScale == 0f)
            Time.timeScale = 1f;

        _active = false;
    }

    private class HitStopRunner : MonoBehaviour { }
}
