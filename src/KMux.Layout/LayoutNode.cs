using System.Text.Json.Serialization;

namespace KMux.Layout;

public enum SplitDirection { Horizontal, Vertical }
public enum NavigationDirection { Up, Down, Left, Right }

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$nodeType")]
[JsonDerivedType(typeof(LeafNode),  "leaf")]
[JsonDerivedType(typeof(SplitNode), "split")]
public abstract record LayoutNode;

public sealed record LeafNode : LayoutNode
{
    public Guid PaneId { get; init; }
    public LeafNode() { PaneId = Guid.NewGuid(); }
    public LeafNode(Guid paneId) { PaneId = paneId; }
}

public sealed record SplitNode(
    SplitDirection Direction,
    double         Ratio,   // fraction for First child (0.0 – 1.0)
    LayoutNode     First,
    LayoutNode     Second
) : LayoutNode;
