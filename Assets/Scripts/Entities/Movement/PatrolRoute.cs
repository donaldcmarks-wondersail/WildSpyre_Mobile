using UnityEngine;

/// <summary>
/// A hand-authored patrol path placed in the scene (never on an enemy prefab).
///
/// Enemies get a route one of two ways:
///   • Scene-placed enemies  → drag a PatrolRoute into CTRL_Enemy in the Inspector.
///   • Spawned enemies       → the spawner calls CTRL_Enemy.AssignPatrolRoute(route).
///
/// Only world positions are read (via <see cref="ToWorldPoints"/>); the enemy never
/// holds references to these Transforms, so a prefab can safely use a spawned route.
/// </summary>
public class PatrolRoute : MonoBehaviour
{
    [Tooltip("Ordered patrol points. Child empty GameObjects work well.")]
    [SerializeField] private Transform[] _points;

    [Header("Gizmo")]
    [SerializeField] private bool _drawLoopClosing = true;
    [SerializeField] private Color _gizmoColor = new Color(0.3f, 0.8f, 1f);

    /// <summary>Number of non-null points.</summary>
    public int Count
    {
        get
        {
            if (_points == null) return 0;
            int n = 0;
            for (int i = 0; i < _points.Length; i++)
                if (_points[i] != null) n++;
            return n;
        }
    }

    /// <summary>Current world positions of every valid point, in order.</summary>
    public Vector2[] ToWorldPoints()
    {
        var result = new Vector2[Count];
        int w = 0;
        if (_points != null)
        {
            for (int i = 0; i < _points.Length; i++)
                if (_points[i] != null) result[w++] = _points[i].position;
        }
        return result;
    }

    private void OnDrawGizmos()
    {
        if (_points == null || _points.Length == 0) return;

        Gizmos.color = _gizmoColor;
        for (int i = 0; i < _points.Length; i++)
        {
            if (_points[i] == null) continue;
            Gizmos.DrawWireSphere(_points[i].position, 0.15f);

            Transform next = null;
            if (i + 1 < _points.Length) next = _points[i + 1];
            else if (_drawLoopClosing && Count > 2) next = FirstValidPoint();

            if (next != null) Gizmos.DrawLine(_points[i].position, next.position);
        }
    }

    private Transform FirstValidPoint()
    {
        for (int i = 0; i < _points.Length; i++)
            if (_points[i] != null) return _points[i];
        return null;
    }
}
