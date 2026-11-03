param([switch]$Package)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
    $compiler = Join-Path $framework 'csc.exe'
    $wpf = Join-Path $framework 'WPF'
    if (!(Test-Path $compiler)) { throw 'Install .NET Framework 4.8 on Windows to build this project.' }
    New-Item -ItemType Directory -Force bin | Out-Null
    $core = @('src\NativeHid.cs', 'src\HidPairingDevice.cs', 'src\PairingProtocol.cs')
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /out:bin\GameSirPairing.exe `
        /resource:src\MainWindow.xaml,MainWindow.xaml /win32manifest:src\app.manifest `
        "/r:$wpf\PresentationFramework.dll" "/r:$wpf\PresentationCore.dll" `
        "/r:$wpf\WindowsBase.dll" /r:System.Xaml.dll @core src\MainWindow.cs
    if ($LASTEXITCODE) { throw 'Application build failed.' }
    & $compiler /nologo /out:bin\ProtocolTests.exe @core tests\ProtocolTests.cs
    if ($LASTEXITCODE) { throw 'Test build failed.' }
    & .\bin\ProtocolTests.exe
    if ($LASTEXITCODE) { throw 'Protocol tests failed.' }
    if ($Package) {
        New-Item -ItemType Directory -Force dist | Out-Null
        Compress-Archive -LiteralPath bin\GameSirPairing.exe,README.md,LICENSE,CHANGELOG.md -DestinationPath dist\GameSirPairing-0.2.0-win-x64.zip -Force
        $sourceFiles = @('src', 'tests', 'docs', '.github', 'build.ps1', 'GameSirPairing.csproj', 'README.md', 'LICENSE', 'CHANGELOG.md', 'CONTRIBUTING.md', '.gitignore')
        Compress-Archive -LiteralPath $sourceFiles -DestinationPath dist\GameSirPairing-0.2.0-source.zip -Force
        Get-FileHash dist\*.zip -Algorithm SHA256 | ForEach-Object { $_.Hash.ToLowerInvariant() + '  ' + (Split-Path $_.Path -Leaf) } | Set-Content dist\SHA256SUMS.txt
    }
} finally { Pop-Location }

