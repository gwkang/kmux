param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "$PSScriptRoot\publish"
)

$project = "$PSScriptRoot\src\KMux.App\KMux.App.csproj"

Write-Host "Building KMux ($Configuration)..." -ForegroundColor Cyan

dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Published to: $OutputDir" -ForegroundColor Green
