namespace sb.Leaflets;

/// <summary>Formatted output lines assigned to one physical column, used internally by
/// <see cref="ColumnBalancer"/>.</summary>
public sealed class Column
{
    public IReadOnlyList<string> Lines { get; }
    public int HeightInLines => Lines.Count;

    public Column(IReadOnlyList<string> lines)
    {
        Lines = lines;
    }
}
