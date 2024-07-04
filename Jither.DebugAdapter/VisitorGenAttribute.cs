namespace Jither.SourceGen;

[AttributeUsage(
    AttributeTargets.Class |
    AttributeTargets.Struct |
    AttributeTargets.Interface |
    AttributeTargets.GenericParameter,
    Inherited = false,
    AllowMultiple = true)]
#pragma warning disable CS9113 // Parameter is unread.
public sealed class VisitorGenAttribute(params Type[] types) : Attribute
#pragma warning restore CS9113 // Parameter is unread.
{
    public bool IncludeAbstract { get; set; }
    public bool SolveConstraints { get; set; } = true;
}
