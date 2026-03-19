param(
    [string]$Configuration = "Debug",
    [switch]$NoBuild
)

$project = "$PSScriptRoot\src\KMux.App\KMux.App.csproj"

$args = @("run", "--project", $project, "-c", $Configuration)
if ($NoBuild) { $args += "--no-build" }

dotnet @args
