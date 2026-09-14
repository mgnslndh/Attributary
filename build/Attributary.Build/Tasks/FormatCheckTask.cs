using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Format;
using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Format-Check")]
[IsDependentOn(typeof(RestoreTask))]
public sealed class FormatCheckTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetFormat("Attributary.sln", new DotNetFormatSettings
        {
            VerifyNoChanges = true,
        });
    }
}
