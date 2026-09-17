using System.Collections;
using UnityEngine;

/// <summary>
/// Shared curve-eased MovePosition tween used by ability anticipation (wind-up pullback,
/// SO_AbilityBase) and ability movement (motion during an ability's own active window).
/// Moves along a fixed direction by a fixed distance, paced per axis by an AnimationCurve
/// evaluated over [0,1] across duration — the curve controls how much of that distance has
/// been covered, not an instantaneous speed. MovePosition (not a direct .position write) so
/// the move is collision-aware and can't teleport the enemy through a wall.
/// </summary>
public static class AbilityMotion
{
    public static IEnumerator MoveCO(EnemyBlackboard board, Vector2 direction, float distance,
                                      float duration, AnimationCurve curveX, AnimationCurve curveY)
    {
        Vector2 startPos = board.rb.position;
        float d = Mathf.Max(duration, 0.0001f);
        float startTime = Time.time;

        while (true)
        {
            float t = Mathf.Clamp01((Time.time - startTime) / d);
            float offsetX = direction.x * distance * curveX.Evaluate(t);
            float offsetY = direction.y * distance * curveY.Evaluate(t);
            board.rb.MovePosition(startPos + new Vector2(offsetX, offsetY));

            if (t >= 1f) yield break;
            yield return null;
        }
    }

    /// <summary>
    /// Waits out duration without moving or evaluating any curve — for a zero-distance
    /// anticipation, where the wind-up should still take time and fire its trigger, but
    /// there's nothing to tween.
    /// </summary>
    public static IEnumerator WaitCO(float duration)
    {
        float d = Mathf.Max(duration, 0.0001f);
        float startTime = Time.time;

        while (Time.time - startTime < d)
            yield return null;
    }
}
