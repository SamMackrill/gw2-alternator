namespace guildwars2.tools.alternator.MVVM.model;

[Serializable]
[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class VPNDetails
{
    public string Id { get; set; }
    public string ConnectionName { get; set; }

    private string DebugDisplay => ToString();

    public override string ToString() => $"{Id} \"{ConnectionName}\"";
}