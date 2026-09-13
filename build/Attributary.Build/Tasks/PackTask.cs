using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Pack;
using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Pack")]
[IsDependentOn(typeof(BuildTask))]
public sealed class PackTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetPack("src/Attributary.Cli/Attributary.Cli.csproj", new DotNetPackSettings
        {
            Configuration = context.Configuration,
            NoRestore = true,
            OutputDirectory = "artifacts",
        });
    }
}
