namespace Depends.Core.Graph
{
    public sealed class SolutionReferenceNode : Node
    {
        public SolutionReferenceNode(string solutionPath) : base(System.IO.Path.GetFileName(solutionPath))
        {
        }

        public override string Type { get; } = "Solution";
    }
}