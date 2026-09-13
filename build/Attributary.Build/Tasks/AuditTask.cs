using System;
using System.Collections.Generic;
using System.Linq;
using Cake.Common;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Audit")]
[IsDependentOn(typeof(RestoreTask))]
public sealed class AuditTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var exitCode = context.StartProcess(
            "dotnet",
            new ProcessSettings
            {
                Arguments = "list Attributary.sln package --vulnerable --include-transitive",
                RedirectStandardOutput = true,
            },
            out IEnumerable<string> redirectedOutput);

        var outputLines = redirectedOutput.ToList();
        foreach (var line in outputLines)
        {
            context.Log.Information(line);
        }

        if (exitCode != 0)
        {
            throw new CakeException($"'dotnet list package --vulnerable' failed with exit code {exitCode}.");
        }

        var projectLines = outputLines
            .Where(line => line.Contains("vulnerable packages", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (projectLines.Count == 0)
        {
            throw new CakeException("'dotnet list package --vulnerable' produced no per-project output; treat this as a tooling failure rather than a clean audit.");
        }

        var flaggedLines = projectLines
            .Where(line => !line.Contains("has no vulnerable packages", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (flaggedLines.Count > 0)
        {
            throw new CakeException(
                "dotnet list package --vulnerable reported vulnerable packages:" + Environment.NewLine +
                string.Join(Environment.NewLine, flaggedLines));
        }
    }
}
