using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.DocFX.DocFXTasks;
using System.Text.Json;
using System.IO;
using static Nuke.Common.ChangeLog.ChangelogTasks;
using Nuke.Common.ChangeLog;
using Nuke.Common.Tools.Coverlet;
using Nuke.Common.Tools.DocFX;

using static Nuke.Common.Tools.SignClient.SignClientTasks;
using Nuke.Common.Tools.SignClient;

using Octokit;
using Nuke.Common.Utilities;

partial class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.Install);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = Configuration.Release;

    [GitRepository] readonly GitRepository GitRepository;

    [Parameter] string NugetPublishUrl = "https://api.nuget.org/v3/index.json";
    
    [Parameter][Secret] string NugetKey;

    [Parameter] int Port = 8090;

    [Parameter] string SymbolsPublishUrl;

    //usage:
    //.\build.cmd createnuget --NugetPrerelease {suffix}
    [Parameter] string NugetPrerelease;

    // Metadata used when signing packages and DLLs
    [Parameter] string SigningName = "RossmannPL.Common.Messaging";
    [Parameter] string SigningDescription = "RossmannPL Messaging lib";
    [Parameter] string SigningUrl = "https://signing.is.cool/";

    [Parameter][Secret] string SignClientSecret;
    [Parameter][Secret] string SignClientUser;
    // Directories
    AbsolutePath ToolsDir => RootDirectory / "tools";
    AbsolutePath Output => RootDirectory / "bin";
    AbsolutePath OutputNuget => Output / "nuget";
    AbsolutePath OutputTests => RootDirectory / "TestResults";
    AbsolutePath OutputPerfTests => RootDirectory / "PerfResults";
    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath DocSiteDirectory => RootDirectory / "docs" / "_site";
    public string ChangelogFile => RootDirectory / "RELEASE_NOTES.md";
    public AbsolutePath DocFxDir => RootDirectory / "docs";
    public AbsolutePath DocFxDirJson => DocFxDir / "docfx.json";

    readonly Solution Solution = ProjectModelTasks.ParseSolution(RootDirectory.GlobFiles("*.sln").FirstOrDefault());

    static readonly JsonElement? _githubContext = string.IsNullOrWhiteSpace(EnvironmentInfo.GetVariable<string>("GITHUB_CONTEXT")) ?
        null
        : JsonSerializer.Deserialize<JsonElement>(EnvironmentInfo.GetVariable<string>("GITHUB_CONTEXT"));

    //let hasTeamCity = (not (buildNumber = "0")) // check if we have the TeamCity environment variable for build # set
    static readonly int BuildNumber = _githubContext.HasValue ? int.Parse(_githubContext.Value.GetProperty("run_number").GetString()) : 0;

    static readonly string PreReleaseVersionSuffix = "beta" + (BuildNumber > 0 ? BuildNumber : DateTime.UtcNow.Ticks.ToString());
    public ChangeLog Changelog => ReadChangelog(ChangelogFile);

    public ReleaseNotes ReleaseNotes => Changelog.ReleaseNotes.OrderByDescending(s => s.Version).FirstOrDefault() ?? throw new ArgumentException("Bad Changelog File. Version Should Exist");

    private string VersionFromReleaseNotes => ReleaseNotes.Version.IsPrerelease ? ReleaseNotes.Version.OriginalVersion : "";
    private string VersionSuffix => NugetPrerelease == "dev" ? PreReleaseVersionSuffix : NugetPrerelease == "" ? VersionFromReleaseNotes : NugetPrerelease;
    public string ReleaseVersion => ReleaseNotes.Version?.ToString() ?? throw new ArgumentException("Bad Changelog File. Define at least one version");
    GitHubClient GitHubClient;
    Target Clean => _ => _
        .Description("Cleans all the output directories")
        .Before(Restore)
        .Executes(() =>
        {
            RootDirectory
            .GlobDirectories("src/**/bin", "src/**/obj", Output, OutputTests, OutputPerfTests, OutputNuget, DocSiteDirectory)
            .ForEach(DeleteDirectory);
            EnsureCleanDirectory(Output);
        });

    Target Restore => _ => _
        .Description("Restores all nuget packages")
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target CreateNuget => _ => _
      .Unlisted()
      .Description("Creates nuget packages")
      .DependsOn(RunTests)
      .Executes(() =>
      {
          var version = ReleaseNotes.Version.ToString();
          var releaseNotes = GetNuGetReleaseNotes(ChangelogFile, GitRepository);

          var projects = SourceDirectory.GlobFiles("**/*.csproj")
          .Except(SourceDirectory.GlobFiles("**/*Tests.csproj", "**/*Tests*.csproj"));
          foreach (var project in projects)
          {
              DotNetPack(s => s
                  .SetProject(project)
                  .SetConfiguration(Configuration)
                  .EnableNoBuild()
                  .EnableIncludeSymbols()
                  .EnableNoRestore()
                  .SetAssemblyVersion(version)
                  .SetFileVersion(version)
                  .SetVersionPrefix(version)
                  .SetVersionSuffix(VersionSuffix)
                  .SetPackageReleaseNotes(releaseNotes)
                  .SetOutputDirectory(OutputNuget));
          }
      });
    Target PublishNuget => _ => _
    .Unlisted()
    .Description("Publishes .nuget packages to Nuget")
    .After(CreateNuget)
    .OnlyWhenDynamic(() => !NugetPublishUrl.IsNullOrEmpty())
    .OnlyWhenDynamic(() => !NugetKey.IsNullOrEmpty())
    .Executes(() =>
    {
        var packages = OutputNuget.GlobFiles("*.nupkg", "*.symbols.nupkg").NotNull();
        var shouldPublishSymbolsPackages = !string.IsNullOrWhiteSpace(SymbolsPublishUrl);
        foreach (var package in packages)
        {
            if (shouldPublishSymbolsPackages)
            {
                DotNetNuGetPush(s => s
                 .SetTimeout(TimeSpan.FromMinutes(10).Minutes)
                 .SetTargetPath(package)
                 .SetSource(NugetPublishUrl)
                 .SetSymbolSource(SymbolsPublishUrl)
                 .SetApiKey(NugetKey));
            }
            else
            {
                DotNetNuGetPush(s => s
                  .SetTimeout(TimeSpan.FromMinutes(10).Minutes)
                  .SetTargetPath(package)
                  .SetSource(NugetPublishUrl)
                  .SetApiKey(NugetKey));
            }

        }
    });
    
    Target RunTests => _ => _
        .Description("Runs all the unit tests")
        .DependsOn(Compile)
        .Executes(() =>
        {
            var projects = Solution.GetProjects("*.Tests");
            foreach (var project in projects)
            {
                Information($"Running tests from {project}");
                foreach (var fw in project.GetTargetFrameworks())
                {
                    Information($"Running for {project} ({fw}) ...");
                    DotNetTest(c => c
                           .SetProjectFile(project)
                           .SetConfiguration(Configuration.ToString())
                           .SetFramework(fw)
                           .SetResultsDirectory(OutputTests)
                           .SetProcessWorkingDirectory(Directory.GetParent(project).FullName)
                           .SetLoggers("trx")
                           .SetVerbosity(verbosity: DotNetVerbosity.Normal)
                           .EnableNoBuild());
                }
            }
        });

    Target Nuget => _ => _
        .DependsOn(CreateNuget, PublishNuget);
    private AbsolutePath[] GetDockerProjects()
    {
        return SourceDirectory.GlobFiles("**/Dockerfile")// folders with Dockerfiles in it
            .ToArray();
    }
    Target PublishCode => _ => _
        .Unlisted()
        .Description("Publish project as release")
        .DependsOn(RunTests)
        .Executes(() =>
        {
            var dockfiles = GetDockerProjects();
            foreach (var dockfile in dockfiles)
            {
                Information(dockfile.Parent.ToString());
                var project = dockfile.Parent.GlobFiles("*.csproj").First();
                DotNetPublish(s => s
                .SetProject(project)
                .SetConfiguration(Configuration.Release));
            }
        });
    Target All => _ => _
     .Description("Executes NBench, Tests and Nuget targets/commands")
     .DependsOn(BuildRelease, RunTests, NBench, Nuget);

    Target NBench => _ => _
     .Description("Runs all BenchMarkDotNet tests")
     .DependsOn(Compile)
     .Executes(() =>
     {
         RootDirectory
             .GlobFiles("src/**/*.Tests.Performance.csproj")
             .ForEach(path =>
             {
                 DotNetRun(s => s
                 .SetApplicationArguments($"--no-build -c release --concurrent true --trace true --output {OutputPerfTests} --diagnostic")
                 .EnableProcessLogOutput()
                 .SetProcessWorkingDirectory(Directory.GetParent(path).FullName)
                 .SetProcessExecutionTimeout((int)TimeSpan.FromMinutes(15).TotalMilliseconds)
                 );
             });
     });
    //--------------------------------------------------------------------------------
    // Documentation 
    //--------------------------------------------------------------------------------
    Target DocsInit => _ => _
        .Unlisted()
        .DependsOn(Compile)
        .Executes(() =>
        {
            DocFXInit(s => s.SetOutputFolder(DocFxDir).EnableQuiet());
        });
    Target DocsMetadata => _ => _
        .Unlisted()
        .Description("Create DocFx metadata")
        .DependsOn(BuildRelease)
        .Executes(() =>
        {
            DocFXMetadata(s => s
            .SetProjects(DocFxDirJson)
            .SetLogLevel(DocFXLogLevel.Verbose));
        });

    Target DocFx => _ => _
        .Description("Builds Documentation")
        .DependsOn(DocsMetadata)
        .Executes(() =>
        {
            DocFXBuild(s => s
            .SetConfigFile(DocFxDirJson)
            .SetLogLevel(DocFXLogLevel.Verbose));
        });

    Target ServeDocs => _ => _
        .Description("Build and preview documentation")
        .DependsOn(DocFx)
        .Executes(() => DocFXServe(s => s.SetFolder(DocFxDir).SetPort(Port)));

    Target Compile => _ => _
        .Description("Builds all the projects in the solution")
        .DependsOn(AssemblyInfo, Restore)
        .Executes(() =>
        {
            var version = ReleaseNotes.Version.ToString();
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target BuildRelease => _ => _
    .DependsOn(Compile);

    Target AssemblyInfo => _ => _
        .After(Restore)
        .Executes(() =>
        {
            XmlTasks.XmlPoke(SourceDirectory / "Directory.Build.props", "//Project/PropertyGroup/PackageReleaseNotes", GetNuGetReleaseNotes(ChangelogFile));
            XmlTasks.XmlPoke(SourceDirectory / "Directory.Build.props", "//Project/PropertyGroup/VersionPrefix", ReleaseVersion);

        });

    Target Install => _ => _
        .Description("Install `Nuke.GlobalTool` and SignClient")
        .Executes(() =>
        {
            DotNetToolInstall(settings => settings.SetPackageName("SignClient").DisableGlobal().SetVersion("1.3.155").SetToolInstallationPath(ToolsDir));
            DotNetToolInstall(settings => settings.SetPackageName("Nuke.GlobalTool").EnableGlobal().SetVersion("1.3.155").SetToolInstallationPath(ToolsDir));
        });

    static void Information(string info)
    {
        Serilog.Log.Information(info);
    }
}
