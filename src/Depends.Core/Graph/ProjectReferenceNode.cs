namespace Depends.Core.Graph
{
    public sealed class ProjectReferenceNode : Node
    {
        public ProjectReferenceNode(string projectPath) : base(System.IO.Path.GetFileName(projectPath))
        {
        }
    }
}
