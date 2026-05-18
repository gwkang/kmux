namespace KMux.Layout;

public class LayoutTree
{
    public LayoutNode Root { get; private set; }

    public LayoutTree() => Root = new LeafNode();
    public LayoutTree(LayoutNode root) => Root = root;

    // ── Mutations ────────────────────────────────────────────────────────────

    /// <summary>Split the leaf containing <paramref name="targetPaneId"/>.</summary>
    /// <returns>ID of the newly created pane.</returns>
    public Guid SplitPane(Guid targetPaneId, SplitDirection dir, double ratio = 0.5)
    {
        var newId = Guid.NewGuid();
        Root = Transform(Root, targetPaneId, leaf =>
            new SplitNode(dir, ratio, leaf, new LeafNode(newId)));
        return newId;
    }

    /// <summary>Remove pane; sibling expands.</summary>
    public void ClosePane(Guid paneId)
    {
        if (Root is LeafNode l && l.PaneId == paneId)
            Root = new LeafNode(); // last pane — replace with fresh one
        else
            Root = Remove(Root, paneId) ?? new LeafNode();
    }

    /// <summary>Update the split ratio for the split containing <paramref name="paneId"/> as its first child.</summary>
    public void ResizePane(Guid paneId, double newRatio)
    {
        Root = UpdateRatio(Root, paneId, Math.Clamp(newRatio, 0.1, 0.9));
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public IEnumerable<Guid> GetAllPaneIds()
    {
        var result = new List<Guid>();
        Collect(Root, result);
        return result;
    }

    public Guid? GetAdjacentPane(Guid paneId, NavigationDirection dir)
    {
        // Build parent map for every node (leaves AND splits) using reference equality,
        // since LayoutNode is a record and value equality would conflate distinct nodes.
        var parents = new Dictionary<LayoutNode, (SplitNode Split, bool IsFirst)>(
            ReferenceEqualityComparer.Instance);
        BuildNodeParentMap(Root, null, false, parents);

        var leaf = FindLeaf(Root, paneId);
        if (leaf is null || !parents.TryGetValue(leaf, out var info)) return null;

        bool wantFirst = dir is NavigationDirection.Up   or NavigationDirection.Left;
        bool needHoriz = dir is NavigationDirection.Up   or NavigationDirection.Down;

        // Walk up the ancestor chain. The direct parent may not have the right split
        // axis (e.g. navigating Left from a pane whose immediate parent is Horizontal).
        // Keep climbing until we find an ancestor whose axis matches and where we are
        // on the opposite side from where we want to go.
        LayoutNode current = leaf;
        while (true)
        {
            bool axisMatch = (info.Split.Direction == SplitDirection.Horizontal) == needHoriz;
            if (axisMatch && wantFirst != info.IsFirst)
            {
                var sibling = info.IsFirst ? info.Split.Second : info.Split.First;
                return FirstLeaf(sibling);
            }
            current = info.Split;
            if (!parents.TryGetValue(current, out info)) return null;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LayoutNode Transform(LayoutNode node, Guid target,
        Func<LeafNode, LayoutNode> fn)
    {
        return node switch
        {
            LeafNode l when l.PaneId == target => fn(l),
            LeafNode l                          => l,
            SplitNode s => s with
            {
                First  = Transform(s.First,  target, fn),
                Second = Transform(s.Second, target, fn)
            },
            _ => node
        };
    }

    private static LayoutNode? Remove(LayoutNode node, Guid target)
    {
        return node switch
        {
            LeafNode l when l.PaneId == target => null,
            LeafNode l                          => l,
            SplitNode s => (Remove(s.First, target), Remove(s.Second, target)) switch
            {
                (null,   var r)  => r,
                (var l,  null)   => l,
                (var l,  var r)  => s with { First = l!, Second = r! }
            },
            _ => node
        };
    }

    private static LayoutNode UpdateRatio(LayoutNode node, Guid firstPaneId, double ratio)
    {
        return node switch
        {
            SplitNode s when Contains(s.First, firstPaneId)
                => s with { Ratio = ratio,
                             First  = UpdateRatio(s.First,  firstPaneId, ratio),
                             Second = UpdateRatio(s.Second, firstPaneId, ratio) },
            SplitNode s
                => s with { First  = UpdateRatio(s.First,  firstPaneId, ratio),
                             Second = UpdateRatio(s.Second, firstPaneId, ratio) },
            _ => node
        };
    }

    private static bool Contains(LayoutNode node, Guid id) =>
        node switch
        {
            LeafNode l  => l.PaneId == id,
            SplitNode s => Contains(s.First, id) || Contains(s.Second, id),
            _           => false
        };

    private static void Collect(LayoutNode node, List<Guid> result)
    {
        if (node is LeafNode l) result.Add(l.PaneId);
        else if (node is SplitNode s) { Collect(s.First, result); Collect(s.Second, result); }
    }

    private static Guid? FirstLeaf(LayoutNode node) =>
        node is LeafNode l ? l.PaneId : node is SplitNode s ? FirstLeaf(s.First) : null;

    private static LeafNode? FindLeaf(LayoutNode node, Guid id) =>
        node switch
        {
            LeafNode l when l.PaneId == id => l,
            SplitNode s => FindLeaf(s.First, id) ?? FindLeaf(s.Second, id),
            _ => null
        };

    private static void BuildNodeParentMap(LayoutNode node, SplitNode? parent, bool isFirst,
        Dictionary<LayoutNode, (SplitNode, bool)> map)
    {
        if (parent != null) map[node] = (parent, isFirst);
        if (node is SplitNode s)
        {
            BuildNodeParentMap(s.First,  s, true,  map);
            BuildNodeParentMap(s.Second, s, false, map);
        }
    }
}
