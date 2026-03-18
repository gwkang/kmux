# Download xterm.js and addons from unpkg CDN
# Run from repo root: pwsh -File download-xterm.ps1

$dest = "src/KMux.App/Assets/xterm"
New-Item -ItemType Directory -Force -Path $dest | Out-Null

$files = @(
    @{ url = "https://unpkg.com/xterm@5.3.0/css/xterm.css";                       out = "xterm.css" },
    @{ url = "https://unpkg.com/xterm@5.3.0/lib/xterm.js";                        out = "xterm.js" },
    @{ url = "https://unpkg.com/@xterm/addon-fit@0.10.0/lib/addon-fit.js";         out = "xterm-addon-fit.js" },
    @{ url = "https://unpkg.com/@xterm/addon-search@0.15.0/lib/addon-search.js";   out = "xterm-addon-search.js" },
    @{ url = "https://unpkg.com/@xterm/addon-web-links@0.11.0/lib/addon-web-links.js"; out = "xterm-addon-web-links.js" }
)

foreach ($f in $files) {
    Write-Host "Downloading $($f.out)..."
    try {
        Invoke-WebRequest -Uri $f.url -OutFile (Join-Path $dest $f.out) -UseBasicParsing
        Write-Host "  OK" -ForegroundColor Green
    } catch {
        Write-Host "  FAILED: $_" -ForegroundColor Red
    }
}

Write-Host "`nDone! Files in $dest"
Get-ChildItem $dest | Format-Table Name, Length
