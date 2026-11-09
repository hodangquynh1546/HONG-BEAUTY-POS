namespace HongBeautyPOS.Domain;

public sealed class AttributeOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Value { get; set; }
    public required string NameKey { get; set; }
    public required string ValueKey { get; set; }
}
