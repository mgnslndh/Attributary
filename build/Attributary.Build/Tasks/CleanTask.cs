using Cake.Common.IO;
using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Clean")]
public sealed class CleanTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.CleanDirectories("src/**/bin");
        context.CleanDirectories("src/**/obj");
        context.CleanDirectories("tests/**/bin");
        context.CleanDirectories("tests/**/obj");
    }
}
