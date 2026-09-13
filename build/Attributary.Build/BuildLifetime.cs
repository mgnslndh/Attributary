using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using Cake.MinVer;

namespace Attributary.Build;

public sealed class BuildLifetime : FrostingLifetime<BuildContext>
{
    public override void Setup(BuildContext context, ISetupContext info)
    {
        var exitCode = context.StartProcess("dotnet", new ProcessSettings
        {
            Arguments = "tool restore",
        });

        if (exitCode != 0)
        {
            throw new CakeException($"'dotnet tool restore' failed with exit code {exitCode}.");
        }

        var minVer = context.MinVer(new MinVerSettings
        {
            TagPrefix = "v",
        });
        context.Version = minVer.Version;

        context.Information($"Building Attributary version {context.Version}");
    }

    public override void Teardown(BuildContext context, ITeardownContext info)
    {
    }
}
