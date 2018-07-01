namespace Depends.Core.Graph
{
    public sealed class AssemblyReferenceNode : Node
    {
        public AssemblyReferenceNode(string assemblyName) : base(assemblyName)
        {
        }

        public override string Type { get; } = "Assembly";
    }
}
