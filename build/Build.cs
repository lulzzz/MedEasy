using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Coverlet;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.ReportGenerator;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Logger;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.ReportGenerator.ReportGeneratorTasks;



[AzurePipelines(
    suffix: "pull-request",
    AzurePipelinesImage.WindowsLatest,
    InvokedTargets = new[] { nameof(Tests) },
    NonEntryTargets = new[] { nameof(Restore) },
    ExcludedTargets = new[] { nameof(Clean) },
    PullRequestsAutoCancel = true,
    PullRequestsBranchesInclude = new[] { "main" },
    TriggerBranchesInclude = new[] {
        "feature/*",
        "fix/*"
    },
    TriggerPathsExclude = new[]
    {
        "docs/*",
        "README.md"
    }
)]

[AzurePipelines(
    AzurePipelinesImage.UbuntuLatest,
    AzurePipelinesImage.WindowsLatest,
    InvokedTargets = new[] { nameof(Tests) },
    NonEntryTargets = new[] { nameof(Restore) },
    ExcludedTargets = new[] { nameof(Clean) },
    PullRequestsAutoCancel = true,
    TriggerBranchesInclude = new[] {
        "main"
    },
    TriggerPathsExclude = new[]
    {
        "docs/*",
        "README.md"
    }
    )]
[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
public class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    public readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    [Parameter("Indicates wheter to restore nuget in interactive mode - Default is false")]
    public readonly bool Interactive = false;

    [Solution] public readonly Solution Solution;
    [GitRepository] public readonly GitRepository GitRepository;

    [CI] public readonly AzurePipelines AzurePipelines;

    [GitVersion] public readonly GitVersion GitVersion;

    [Partition(10)] public readonly Partition TestPartition;

    public AbsolutePath SourceDirectory => RootDirectory / "src";
    public AbsolutePath TestDirectory => RootDirectory / "test";

    public AbsolutePath OutputDirectory => RootDirectory / "output";

    public AbsolutePath CoverageReportDirectory => OutputDirectory / "coverage-report";

    public AbsolutePath TestResultDirectory => OutputDirectory / "tests-results";

    public AbsolutePath ArtifactsDirectory => OutputDirectory / "artifacts";
    public AbsolutePath CoverageReportHistoryDirectory => OutputDirectory / "coverage-history";

    private const string CsProjGlobFilesPattern = "**/*.csproj";

    public Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            TestDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(CoverageReportDirectory);
            EnsureCleanDirectory(TestResultDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    public Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            IEnumerable<AbsolutePath> projects = SourceDirectory.GlobFiles(CsProjGlobFilesPattern)
                                                                .Concat(TestDirectory.GlobFiles(CsProjGlobFilesPattern))
                                                                .Where(path => !path.ToString().Like("*.SPA.csproj"));

            Trace($"Projects : {string.Join("\n", projects)}");

            DotNetRestore(s => s
                .SetConfigFile("nuget.config")
                .SetIgnoreFailedSources(true)
                .When(IsLocalBuild && Interactive, _ => _.SetProperty("NugetInteractive", IsLocalBuild && Interactive))
                .CombineWith(projects, (setting, project) => setting.SetProjectFile(project)
                                                                    .SetVerbosity(DotNetVerbosity.Minimal))
            );
        });

    public Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetConfiguration(Configuration)
                .SetProjectFile(Solution)
                .SetNoRestore(InvokedTargets.Contains(Restore))
                );
        });

    public Target UnitTests => _ => _
        .DependsOn(Compile)
        .Description("Run unit tests and collect code")
        .Produces(TestResultDirectory / "*-unit-tests.trx")
        .Produces(TestResultDirectory / "*-unit-tests.xml")
        .Produces(CoverageReportHistoryDirectory / "*.xml")
        .Executes(() =>
        {
            IEnumerable<Project> projects = Solution.GetProjects("*.UnitTests");
            IEnumerable<Project> testsProjects = TestPartition.GetCurrent(projects);

            testsProjects.ForEach(project => Info(project));

            DotNetTest(s => s
                .SetConfiguration(Configuration)
                .EnableCollectCoverage()
                .EnableUseSourceLink()
                .SetNoBuild(InvokedTargets.Contains(Compile))
                .AddProperty("maxcpucount", "1")
                .SetResultsDirectory(TestResultDirectory)
                .SetCoverletOutputFormat(CoverletOutputFormat.cobertura)
                .AddProperty("ExcludeByAttribute", "Obsolete")
                .CombineWith(testsProjects, (cs, project) => cs.SetProjectFile(project)
                    .CombineWith(project.GetTargetFrameworks(), (setting, framework) => setting
                        .SetFramework(framework)
                        .SetLogger($"trx;LogFileName={project.Name}-unit-tests.{framework}.trx")
                        .SetCollectCoverage(true)
                        .SetCoverletOutput(TestResultDirectory / $"{project.Name}-unit-tests.xml"))
                    )
            );

            TestResultDirectory.GlobFiles("*-unit-tests.*.trx")
                               .ForEach(testFileResult => AzurePipelines?.PublishTestResults(type: AzurePipelinesTestResultsType.VSTest,
                                                                                             title: $"{Path.GetFileNameWithoutExtension(testFileResult)} ({AzurePipelines.StageDisplayName})",
                                                                                             files: new string[] { testFileResult })
            );

        });

    public Target IntegrationTests => _ => _
        .DependsOn(Compile)
        .Description("Run integration tests and test results coverage")
        .Partition(() => TestPartition)
        .Produces(TestResultDirectory / "*-integration-tests.trx")
        .Produces(TestResultDirectory / "*-integration-tests.xml")
        .Executes(() =>
        {
            IEnumerable<Project> projects = Solution.GetProjects("*.IntegrationTests");
            IEnumerable<Project> testsProjects = TestPartition.GetCurrent(projects);

            DotNetTest(s => s
                 .SetConfiguration(Configuration)
                 .EnableCollectCoverage()
                 .EnableUseSourceLink()
                 .SetNoBuild(InvokedTargets.Contains(Compile))
                 .When(IsServerBuild, _ => _.AddProperty("maxcpucount", "1"))
                 .SetResultsDirectory(TestResultDirectory)
                 .SetCoverletOutputFormat(CoverletOutputFormat.cobertura)
                 .AddProperty("ExcludeByAttribute", "Obsolete")
                 .CombineWith(testsProjects, (cs, project) => cs.SetProjectFile(project)
                     .CombineWith(project.GetTargetFrameworks(), (setting, framework) => setting
                         .SetFramework(framework)
                         .SetLogger($"trx;LogFileName={project.Name}-integration-tests.{framework}.trx")
                         .SetCollectCoverage(true)
                         .SetCoverletOutput(TestResultDirectory / $"{project.Name}-integration-tests.xml"))
                     )
             );

            TestResultDirectory.GlobFiles("*-integration-tests.*.trx")
                               .ForEach(testFileResult => AzurePipelines?.PublishTestResults(type: AzurePipelinesTestResultsType.VSTest,
                                                                                             title: $"{Path.GetFileNameWithoutExtension(testFileResult)} ({AzurePipelines.StageDisplayName})",
                                                                                             files: new string[] { testFileResult })
            );
        });

    public Target Tests => _ => _
        .DependsOn(UnitTests, IntegrationTests)
        .Triggers(Coverage)
        .Executes(() =>
        {

        });

    public Target Coverage => _ => _
        .DependsOn(Tests)
        .Consumes(Tests)
        .Executes(() =>
        {

            // TODO Move this to a separate "coverage" target once https://github.com/nuke-build/nuke/issues/562 is solved !
            ReportGenerator(_ => _
                .SetFramework("net5.0")
                .SetReports(TestResultDirectory / "*.xml")
                .SetReportTypes(ReportTypes.Badges, ReportTypes.HtmlChart, ReportTypes.HtmlInline_AzurePipelines_Dark)
                .SetTargetDirectory(CoverageReportDirectory)
                .SetHistoryDirectory(CoverageReportHistoryDirectory)
            );

            TestResultDirectory.GlobFiles("*.xml")
                               .ForEach(file => AzurePipelines?.PublishCodeCoverage(coverageTool: AzurePipelinesCodeCoverageToolType.Cobertura,
                                                                                    summaryFile: file,
                                                                                    reportDirectory: CoverageReportDirectory));
        });

    public Target Publish => _ => _
        .DependsOn(UnitTests, IntegrationTests)
        .Executes(() =>
        {
            DotNetPublish(s => s
                .SetProject(Solution)
                .SetNoBuild(InvokedTargets.Contains(Compile))
                .SetNoRestore(InvokedTargets.Contains(Restore))
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .SetVersion(GitVersion.NuGetVersion)
                .SetOutput(ArtifactsDirectory)
            );
        })
    ;

    protected override void OnTargetStart(string target)
    {
        Info($"Starting '{target}' task");
    }

    protected override void OnTargetExecuted(string target)
    {
        Info($"'{target}' task finished");
    }

    protected override void OnBuildInitialized()
    {
        Info($"{nameof(BuildProjectDirectory)} : {BuildProjectDirectory}");
    }

}
