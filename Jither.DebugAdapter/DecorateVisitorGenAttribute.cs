namespace Jither.SourceGen;

/// <summary>
/// Generate given attributes on the target, 
/// using types found by <see cref="VisitorGenAttribute"/>.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class |
    AttributeTargets.Struct |
    AttributeTargets.Interface, Inherited = false, AllowMultiple = true)]
public sealed class DecorateVisitorGenAttribute(params Type[] attributeTypes) : Attribute
{
    public string Suffix { get; set; }
}