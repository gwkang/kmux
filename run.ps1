param(
    [string]$Configuration = "Debug",
    [switch]$NoBuild
)

$project = "$PSScriptRoot\src\KMux.App\KMux.App.csproj"

$dotnetArgs = @("run", "--project", $project, "-c", $Configuration)
if ($NoBuild) { $dotnetArgs += "--no-build" }

dotnet @dotnetArgs
