using Cake.Common;
using Cake.Common.Tools.DotNet;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Restore")]
public sealed class RestoreTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetRestore("Attributary.sln");

        var exitCode = context.StartProcess("dotnet", new ProcessSettings
        {
            Arguments = "tool restore",
        });

        if (exitCode != 0)
        {
            throw new CakeException($"'dotnet tool restore' failed with exit code {exitCode}.");
        }
    }
}
