using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Test")]
[IsDependentOn(typeof(BuildTask))]
public sealed class TestTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var testResultsRoot = context.MakeAbsolute(context.Directory("TestResults"));
        var mergedReportDir = testResultsRoot.Combine("merged");

        var testProjects = context.GetFiles("tests/**/*.csproj").ToList();
        if (testProjects.Count == 0)
        {
            throw new CakeException("No test projects found under 'tests'.");
        }

        var coverageFiles = new List<FilePath>();
        var anyTestsFailed = false;

        foreach (var project in testProjects)
        {
            var projectName = project.GetFilenameWithoutExtension().ToString();
            var projectResultsDir = testResultsRoot.Combine(projectName);

            if (context.DirectoryExists(projectResultsDir))
            {
                context.DeleteDirectory(projectResultsDir, new DeleteDirectorySettings { Recursive = true, Force = true });
            }

            var exitCode = context.StartProcess("dotnet", new ProcessSettings
            {
                Arguments = new ProcessArgumentBuilder()
                    .Append("test")
                    .AppendQuoted(project.FullPath)
                    .Append("--no-build")
                    .Append("--configuration").Append(context.Configuration)
                    .Append("--coverage")
                    .Append("--coverage-output-format").Append("cobertura")
                    .Append("--results-directory").AppendQuoted(projectResultsDir.FullPath),
            });

            if (exitCode != 0)
            {
                anyTestsFailed = true;
            }

            var coverageFile = context.GetFiles(projectResultsDir.FullPath + "/**/*.cobertura.xml").FirstOrDefault();
            if (coverageFile is null)
            {
                context.Warning($"No .cobertura.xml was produced for '{projectName}' under '{projectResultsDir}'.");
                continue;
            }

            coverageFiles.Add(coverageFile);
        }

        if (coverageFiles.Count == 0)
        {
            throw new CakeException("No coverage data was produced by any test project.");
        }

        if (context.DirectoryExists(mergedReportDir))
        {
            context.DeleteDirectory(mergedReportDir, new DeleteDirectorySettings { Recursive = true, Force = true });
        }

        var reportsArg = string.Join(";", coverageFiles.Select(f => f.FullPath));
        var reportGenExitCode = context.StartProcess("dotnet", new ProcessSettings
        {
            Arguments = new ProcessArgumentBuilder()
                .Append("tool").Append("run").Append("reportgenerator")
                .Append("--")
                .AppendQuoted($"-reports:{reportsArg}")
                .AppendQuoted($"-targetdir:{mergedReportDir.FullPath}")
                .Append("-reporttypes:TextSummary"),
        });

        if (reportGenExitCode != 0)
        {
            throw new CakeException($"reportgenerator failed with exit code {reportGenExitCode}.");
        }

        var summaryFile = mergedReportDir.CombineWithFilePath("Summary.txt");
        if (!context.FileExists(summaryFile))
        {
            throw new CakeException($"reportgenerator did not produce a summary at '{summaryFile}'.");
        }

        context.Information(string.Empty);
        context.Information(File.ReadAllText(summaryFile.FullPath));

        if (anyTestsFailed)
        {
            throw new CakeException("One or more test projects had failing tests -- see output above.");
        }
    }
}
