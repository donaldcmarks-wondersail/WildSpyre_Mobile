using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

/// <summary>
/// Sits on a NodeLink2 (Add Component → Pathfinding → Link2) and carries the
/// platformer jump parameters that CTRL_EnemyMover uses to actually cross the link.
///
///   • NodeLink2      → routing. The pathfinder plans a route over the gap.
///   • PlatformJumpLink + CTRL_EnemyMover → execution. Launches the jump arc.
///
/// Setup:
///   1. Empty GameObject at the take-off edge, in the walkable band above the platform.
///   2. Add Component → Pathfinding → Link2. Set its End to a GameObject at the landing.
///   3. Add this component. Set requiredJumpForce to the enemy's SO_EnemyMovement.jumpForce.
///   4. Scan the AstarPath graph.
/// </summary>
[RequireComponent(typeof(NodeLink2))]
public class PlatformJumpLink : MonoBehaviour
{
    [Header("Jump Parameters")]
    [Tooltip("Upward launch velocity. Match (or slightly exceed) the enemy's SO_EnemyMovement.jumpForce.")]
    public float requiredJumpForce = 8f;

    [Tooltip("Horizontal speed held during the arc, as a multiple of the enemy's current move speed.")]
    public float horizontalBoostMultiplier = 1f;

    [Header("Gizmos")]
    public bool drawGizmo = true;
    public Color gizmoColor = Color.yellow;

    /// <summary>All enabled jump links in the scene.</summary>
    public static readonly List<PlatformJumpLink> All = new List<PlatformJumpLink>();

    private NodeLink2 _link;

    private void Awake() => _link = GetComponent<NodeLink2>();

    private void OnEnable()
    {
        if (_link == null) _link = GetComponent<NodeLink2>();
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable() => All.Remove(this);

    /// <summary>Take-off world position (this GameObject).</summary>
    public Vector2 StartPos => transform.position;

    /// <summary>Landing world position (the NodeLink2's End transform).</summary>
    public Vector2 EndPos =>
        _link != null && _link.end != null ? (Vector2)_link.end.position : (Vector2)transform.position;

    /// <summary>True once the link has a landing transform assigned.</summary>
    public bool HasLanding => _link != null && _link.end != null;

    /// <summary>The sibling NodeLink2 this jump link decorates.</summary>
    public NodeLink2 Link => _link != null ? _link : (_link = GetComponent<NodeLink2>());

    private void OnDrawGizmos()
    {
        if (!drawGizmo) return;

        NodeLink2 link = _link != null ? _link : GetComponent<NodeLink2>();
        Vector3 a = transform.position;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(a, 0.2f);

        if (link == null || link.end == null) return;

        Vector3 b = link.end.position;
        Gizmos.DrawWireSphere(b, 0.2f);

        // Parabolic preview of the jump arc.
        float apex = Vector3.Distance(a, b) * 0.35f;
        Vector3 prev = a;
        for (int i = 1; i <= 16; i++)
        {
            float t = i / 16f;
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y += Mathf.Sin(t * Mathf.PI) * apex;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
