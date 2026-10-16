$ErrorActionPreference = 'Stop'
if (![System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) { throw 'Only win-x64 is currently packaged.' }
$root = Split-Path $PSScriptRoot -Parent
$config = Get-Content (Join-Path $PSScriptRoot 'native.json') -Raw | ConvertFrom-Json
$commit = $config.commit
$archiveHash = $config.archiveSha256
$cache = Join-Path $root 'artifacts/upstream'
$upstream = Join-Path $cache "RaTeX-$commit"
New-Item -ItemType Directory -Force -Path $cache | Out-Null
if (!(Test-Path -LiteralPath $upstream)) {
    $archive = Join-Path $cache 'ratex.tar.gz'
    Invoke-WebRequest "https://codeload.github.com/erweixin/RaTeX/tar.gz/$commit" -OutFile $archive
    if ((Get-FileHash $archive -Algorithm SHA256).Hash -ne $archiveHash) { throw 'Upstream archive hash mismatch.' }
    tar -xzf $archive -C $cache
    if ($LASTEXITCODE) { throw 'Upstream extraction failed.' }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Cargo.lock') -Destination (Join-Path $upstream 'Cargo.lock')
$savedFlags = $env:RUSTFLAGS
$savedEncoded = $env:CARGO_ENCODED_RUSTFLAGS
$savedWrapper = $env:RUSTC_WRAPPER
Push-Location $upstream
try {
    $env:RUSTFLAGS = ''
    $env:CARGO_ENCODED_RUSTFLAGS = ''
    $env:RUSTC_WRAPPER = ''
    cargo "+$($config.toolchain)" build --locked --release -p ratex-ffi --target $config.target --target-dir (Join-Path $upstream 'target')
    if ($LASTEXITCODE) { throw 'Native build failed.' }
    & (Join-Path $PSScriptRoot 'update-notices.ps1')
    $target = Join-Path $root 'src/RaTeX.Net/runtimes/win-x64/native'
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -LiteralPath "target/$($config.target)/release/ratex_ffi.dll" -Destination $target
    Get-FileHash (Join-Path $target 'ratex_ffi.dll') -Algorithm SHA256
} finally {
    Pop-Location
    $env:RUSTFLAGS = $savedFlags
    $env:CARGO_ENCODED_RUSTFLAGS = $savedEncoded
    $env:RUSTC_WRAPPER = $savedWrapper
}
