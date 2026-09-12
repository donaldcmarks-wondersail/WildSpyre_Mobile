using Pathfinding;
using UnityEngine;

/// <summary>
/// Makes grid-graph nodes with no ground beneath them unwalkable, after every scan —
/// for the ground graph ONLY.
///
/// A 2D grid graph using 2D physics has height testing disabled (Unity's 2D API
/// cannot do it), so it marks ALL empty space walkable. Pathfinding then routes a
/// walking enemy straight across a pit — it steps off the ledge and falls, and
/// jump links are never used because A* thinks it can just walk across.
///
/// This raycasts straight down from every node and keeps only the ones sitting
/// within <see cref="groundCheckDistance"/> above a Ground-layer collider: a thin
/// band hugging each platform surface. Gaps become genuinely impassable, so A*
/// has to route through a NodeLink2 to cross them — and the jump actually fires.
///
/// Flying enemies need the opposite (all air walkable), so they use a SEPARATE
/// grid graph that this filter must not touch. Set <see cref="groundGraphName"/> to
/// the name of the walker graph; the air graph is left alone. If the name matches
/// nothing, this falls back to the first grid graph (single-graph projects).
///
/// Setup: drop this on any GameObject in the scene, set Ground Mask + Ground Graph
/// Name, and Scan. It re-runs automatically on every scan / graph update.
/// </summary>
[AddComponentMenu("Pathfinding/Grid Ground Filter")]
public class GridGroundFilter : GraphModifier
{
    [Tooltip("Name of the grid graph to filter (the walker/ground graph). Any other " +
             "grid graph — e.g. the flyer's air graph — is left untouched. Blank or " +
             "unmatched falls back to the first grid graph.")]
    public string groundGraphName = "Ground";

    [Tooltip("A node stays walkable only if solid ground is within this distance straight below it. " +
             "Start near (node size + half the enemy height); raise if ledge/jump nodes get culled, " +
             "lower if gaps stay walkable.")]
    public float groundCheckDistance = 1.25f;

    [Tooltip("Layers counted as stand-on ground. Match the grid graph's collision mask (Ground).")]
    public LayerMask groundMask;

    [Tooltip("Log how many nodes were culled on each scan.")]
    public bool logResult = true;

    public override void OnPostScan() => Filter();
    public override void OnGraphsPostUpdateBeforeAreaRecalculation() => Filter();

    [ContextMenu("Re-scan graph now")]
    private void Rescan()
    {
        if (AstarPath.active != null) AstarPath.active.Scan();
    }

    private void Filter()
    {
        if (AstarPath.active == null) return;

        GridGraph gg = ResolveGraph();
        if (gg == null || gg.nodes == null) return;

        int culled = 0;
        for (int i = 0; i < gg.nodes.Length; i++)
        {
            GridNodeBase node = gg.nodes[i];
            if (node == null || !node.Walkable) continue;

            Vector2 p = (Vector3)node.position;
            if (Physics2D.Raycast(p, Vector2.down, groundCheckDistance, groundMask).collider == null)
            {
                node.Walkable = false;
                culled++;
            }
        }

        gg.RecalculateConnectionsInRegion(new IntRect(0, 0, gg.width - 1, gg.depth - 1));

        if (logResult)
            Debug.Log($"[GridGroundFilter] '{gg.name}': culled {culled} airborne nodes " +
                      $"(groundCheckDistance={groundCheckDistance}, mask={groundMask.value})", this);
    }

    private GridGraph ResolveGraph()
    {
        if (!string.IsNullOrEmpty(groundGraphName))
        {
            NavGraph named = AstarPath.active.data.FindGraph(g => g.name == groundGraphName);
            if (named is GridGraph namedGrid) return namedGrid;

            Debug.LogWarning($"[GridGroundFilter] No grid graph named '{groundGraphName}' — " +
                             $"filtering the first grid graph instead. If you have a separate air " +
                             $"graph for flyers, this will wrongly cull it.", this);
        }
        return AstarPath.active.data.gridGraph;   // fallback: first grid graph
    }
}
