param([string]$Version = '0.3.2')
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

# Publish a portable Windows app with .NET included; WebView2 remains shared.
dotnet publish src/MarkdownPreview/MarkdownPreview.csproj -c Release -r win-x64 --self-contained true -p:Version=$Version -o artifacts/win-x64
if ($LASTEXITCODE -ne 0) { throw 'Publishing failed.' }
Copy-Item README.md artifacts/win-x64/README.md
Copy-Item THIRD-PARTY-NOTICES.md artifacts/win-x64/THIRD-PARTY-NOTICES.md
Copy-Item scripts/diagnose-windows.ps1 artifacts/win-x64/diagnose-windows.ps1
Compress-Archive -Path artifacts/win-x64/* -DestinationPath "artifacts/NeoNote-$Version-win-x64.zip" -Force

# Also offer a smaller package for machines with .NET 10 Desktop Runtime installed.
dotnet publish src/MarkdownPreview/MarkdownPreview.csproj -c Release -r win-x64 --self-contained false -p:Version=$Version -o artifacts/win-x64-framework
if ($LASTEXITCODE -ne 0) { throw 'Framework-dependent publishing failed.' }
Copy-Item README.md artifacts/win-x64-framework/README.md
Copy-Item THIRD-PARTY-NOTICES.md artifacts/win-x64-framework/THIRD-PARTY-NOTICES.md
Copy-Item scripts/diagnose-windows.ps1 artifacts/win-x64-framework/diagnose-windows.ps1
Compress-Archive -Path artifacts/win-x64-framework/* -DestinationPath "artifacts/NeoNote-$Version-win-x64-framework.zip" -Force

# Produce a directly runnable EXE; bundled files are extracted on its first launch.
dotnet publish src/MarkdownPreview/MarkdownPreview.csproj -c Release -r win-x64 --self-contained true -p:Version=$Version -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o artifacts/single-file
if ($LASTEXITCODE -ne 0) { throw 'Single-file publishing failed.' }
Copy-Item artifacts/single-file/NeoNote.exe "artifacts/NeoNote-$Version-win-x64-portable.exe"

# Build the per-user installer using the signed Inno Setup installation.
$compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if (-not $compiler) {
    $compilerPath = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
    if (-not (Test-Path $compilerPath)) { throw 'Install Inno Setup 6 to build the installer.' }
} else { $compilerPath = $compiler.Source }
& $compilerPath "/DAppVersion=$Version" "/DPublishDirectory=$((Resolve-Path artifacts/win-x64).Path)" packaging/MarkdownPreview.iss
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }

# Record checksums of distributable files rather than build intermediates.
Get-ChildItem artifacts -File | Where-Object { $_.Extension -in '.zip', '.exe' } | ForEach-Object {
    $hash = Get-FileHash $_.FullName -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $($_.Name)"
} | Set-Content artifacts/SHA256SUMS.txt
