//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// SET PACKAGE VERSION
//////////////////////////////////////////////////////////////////////

var version = "0.9.1";
var modifier = "";

var isAppveyor = BuildSystem.IsRunningOnAppVeyor;
var packageVersion = version + modifier;
var dbgSuffix = configuration == "Debug" ? "-dbg" : "";

//////////////////////////////////////////////////////////////////////
// DEFINE RUN CONSTANTS
//////////////////////////////////////////////////////////////////////

var PROJECT_DIR = Context.Environment.WorkingDirectory.FullPath + "/";
var PACKAGE_DIR = PROJECT_DIR + "package/";
var TOOLS_DIR = PROJECT_DIR + "tools/";
var BIN_DIR = PROJECT_DIR + "bin/" + configuration + "/";

var SOLUTION = PROJECT_DIR + "nunit-editor.sln";
var TESTS_NAME = "nunit-editor.tests.dll";
var TESTS = BIN_DIR + TESTS_NAME;

var NUNIT3_CONSOLE = TOOLS_DIR + "NUnit.ConsoleRunner/tools/nunit3-console.exe";

//////////////////////////////////////////////////////////////////////
// CLEAN
//////////////////////////////////////////////////////////////////////

Task("Clean")
.Does(() =>
{
    CleanDirectory(BIN_DIR);
});

//////////////////////////////////////////////////////////////////////
// INITIALIZE FOR BUILD
//////////////////////////////////////////////////////////////////////

Task("InitializeBuild")
.Does(() =>
{
    NuGetRestore(SOLUTION);

    if (isAppveyor)
    {
        var tag = AppVeyor.Environment.Repository.Tag;

        if (tag.IsTag)
        {
            packageVersion = tag.Name;
        }
        else
        {
            var buildNumber = AppVeyor.Environment.Build.Number;
            packageVersion = version + "-CI-" + buildNumber + dbgSuffix;
            if (AppVeyor.Environment.PullRequest.IsPullRequest)
            packageVersion += "-PR-" + AppVeyor.Environment.PullRequest.Number;
            else
            packageVersion += "-" + AppVeyor.Environment.Repository.Branch;
        }

        AppVeyor.UpdateBuildVersion(packageVersion);
    }
});

//////////////////////////////////////////////////////////////////////
// BUILD
//////////////////////////////////////////////////////////////////////

Task("Build")
.IsDependentOn("InitializeBuild")
.Does(() =>
{
    if(IsRunningOnWindows())
    {
        MSBuild(SOLUTION, new MSBuildSettings()
            .SetConfiguration(configuration)
            .SetVerbosity(Verbosity.Minimal)
            .SetNodeReuse(false));
    }
    else
    {
        XBuild(SOLUTION, new XBuildSettings()
            .SetConfiguration(configuration)
            .SetVerbosity(Verbosity.Minimal)
            .WithTarget("Build"));
    }
});

//////////////////////////////////////////////////////////////////////
// TEST
//////////////////////////////////////////////////////////////////////

Task("Test")
.IsDependentOn("Build")
.Does(() =>
{
    int rc = StartProcess(NUNIT3_CONSOLE, new ProcessSettings()
    {
        Arguments = TESTS
    });

    if (rc != 0)
        throw new Exception(string.Format("NUnit test run for {0} returned code {1}.", TESTS_NAME, rc)); 
});

//////////////////////////////////////////////////////////////////////
// PACKAGE
//////////////////////////////////////////////////////////////////////

Task("PackageSource")
.Does(() =>
{
    var path = PACKAGE_DIR + "NUnit-Project-Editor-" + packageVersion + "-src.zip";

    CreateDirectory(PACKAGE_DIR);
    
    RunGitCommand(string.Format("archive -o {0} HEAD", path));
});

Task("PackageZip")
.Does(() =>
{
    var path = PACKAGE_DIR + "NUnit-Project-Editor-" + packageVersion + dbgSuffix + ".zip";

    CreateDirectory(PACKAGE_DIR);

    CopyFileToDirectory("LICENSE.txt", BIN_DIR);
    CopyFileToDirectory("CHANGES.txt", BIN_DIR);

    var zipFiles = new FilePath[]
    {
        BIN_DIR + "LICENSE.txt",
        BIN_DIR + "CHANGES.txt",
        BIN_DIR + "nunit-editor.exe",
        BIN_DIR + "nunit.ico"
    };

    Zip(BIN_DIR, File(path), zipFiles);
});

//////////////////////////////////////////////////////////////////////
// HELPER METHODS
//////////////////////////////////////////////////////////////////////

void RunGitCommand(string arguments)
{
    StartProcess("git", new ProcessSettings()
    {
        Arguments = arguments
    });
}

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Rebuild")
.IsDependentOn("Clean")
.IsDependentOn("Build");

Task("Package")
.IsDependentOn("PackageSource")
.IsDependentOn("PackageZip");

Task("Appveyor")
.IsDependentOn("Build")
.IsDependentOn("Test")
.IsDependentOn("Package");

Task("Travis")
.IsDependentOn("Build")
.IsDependentOn("Test");

Task("Default")
.IsDependentOn("Build");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);