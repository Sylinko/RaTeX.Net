#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
if (!$IsWindows) {
    throw 'Build Windows native assets on Windows.'
}

$RuntimeIdentifier = 'win-x64'
$root = Split-Path $PSScriptRoot -Parent
$config = Get-Content (Join-Path $PSScriptRoot 'native.json') -Raw | ConvertFrom-Json
$nativeTarget = $config.targets.$RuntimeIdentifier
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
rustup target add $nativeTarget.target --toolchain $config.toolchain
if ($LASTEXITCODE) { throw 'Rust target installation failed.' }
$savedFlags = $env:RUSTFLAGS
$savedEncoded = $env:CARGO_ENCODED_RUSTFLAGS
$savedWrapper = $env:RUSTC_WRAPPER
Push-Location $upstream
try {
    $env:RUSTFLAGS = ''
    $env:CARGO_ENCODED_RUSTFLAGS = ''
    $env:RUSTC_WRAPPER = ''
    cargo "+$($config.toolchain)" build --locked --release -p ratex-ffi --target $nativeTarget.target --target-dir (Join-Path $upstream 'target')
    if ($LASTEXITCODE) { throw 'Native build failed.' }
    & (Join-Path $PSScriptRoot 'update-windows-notices.ps1')
    $target = Join-Path $root "src/RaTeX/runtimes/$RuntimeIdentifier/native"
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -LiteralPath "target/$($nativeTarget.target)/release/$($nativeTarget.library)" -Destination $target
    Get-FileHash (Join-Path $target $nativeTarget.library) -Algorithm SHA256
} finally {
    Pop-Location
    $env:RUSTFLAGS = $savedFlags
    $env:CARGO_ENCODED_RUSTFLAGS = $savedEncoded
    $env:RUSTC_WRAPPER = $savedWrapper
}
