$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet publish windows/LGInputSwitch/LGInputSwitch.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o outputs/windows-x64
if ($LASTEXITCODE -ne 0) { throw 'A Windows build nem sikerült.' }
New-Item -ItemType Directory -Force outputs/windows-x64/Licenses | Out-Null
Copy-Item LICENSE outputs/windows-x64/Licenses/LGInputSwitch.txt
Copy-Item third_party/licenses/*.txt outputs/windows-x64/Licenses/
Copy-Item README.md outputs/windows-x64/
Copy-Item LICENSE outputs/windows-x64/
New-Item -ItemType Directory -Force outputs/windows-x64/docs | Out-Null
Copy-Item docs/* outputs/windows-x64/docs/ -Recurse -Force
# Preserve the licences shipped with the exact runtime packages used by restore.
$assets = Get-Content windows/LGInputSwitch/obj/project.assets.json -Raw | ConvertFrom-Json
foreach ($package in @('Microsoft.NETCore.App.Runtime.win-x64', 'Microsoft.WindowsDesktop.App.Runtime.win-x64')) {
    $dependency = $assets.project.frameworks.PSObject.Properties | ForEach-Object { $_.Value.downloadDependencies } | Where-Object { $_.name -ieq $package } | Select-Object -First 1
    if (-not $dependency) { throw "Missing runtime package: $package" }
    $version = $dependency.version.TrimStart('[').Split(',')[0].Trim()
    $identity = "$($package.ToLowerInvariant())/$version"
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $packagePath = Join-Path $folder $identity.ToLowerInvariant()
        if (Test-Path $packagePath) {
            Get-ChildItem $packagePath -File | Where-Object { $_.Name -imatch '^(LICENSE(\.TXT)?|THIRD-PARTY-NOTICES(\.TXT)?)$' } | ForEach-Object {
                Copy-Item $_.FullName (Join-Path outputs/windows-x64/Licenses "$package-$($_.Name)")
            }
            break
        }
    }
}
Compress-Archive -Path outputs/windows-x64/* -DestinationPath outputs/LGInputSwitch-Windows-x64.zip -Force
