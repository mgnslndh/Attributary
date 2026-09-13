using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Default")]
[IsDependentOn(typeof(RestoreTask))]
[IsDependentOn(typeof(BuildTask))]
[IsDependentOn(typeof(FormatCheckTask))]
[IsDependentOn(typeof(AuditTask))]
[IsDependentOn(typeof(TestTask))]
public sealed class DefaultTask : FrostingTask<BuildContext>
{
}
