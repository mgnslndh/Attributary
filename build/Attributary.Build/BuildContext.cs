using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Core;
using Cake.Frosting;
using Cake.MinVer;

namespace Attributary.Build;

public sealed class BuildContext : FrostingContext
{
    public new string Configuration { get; }
    public string Version { get; }

    public BuildContext(ICakeContext context)
        : base(context)
    {
        Configuration = context.Argument("configuration", "Release");

        var minVer = context.MinVer(new MinVerSettings
        {
            TagPrefix = "v",
        });
        Version = minVer.Version;

        context.Information($"Building Attributary version {Version}");
    }
}
