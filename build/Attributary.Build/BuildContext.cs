using Cake.Common;
using Cake.Core;
using Cake.Frosting;

namespace Attributary.Build;

public sealed class BuildContext : FrostingContext
{
    public new string Configuration { get; }
    public string Version { get; set; } = string.Empty;

    public BuildContext(ICakeContext context)
        : base(context)
    {
        Configuration = context.Argument("configuration", "Release");
    }
}
