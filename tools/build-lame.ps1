# Builds libmp3lame.so for every Android ABI the app ships, using the NDK's clang.
#
# The MP3 button encodes on the phone with LAME. The resulting libraries are committed
# under src/LinkAudioPlayer.App/Platforms/Android/lib, so a normal build never needs
# this script. Run it only to upgrade LAME or change the compiler flags.
#
# Usage:
#   1. Download lame-3.100.tar.gz from https://lame.sourceforge.io and check its
#      SHA-256 is ddfe36cab873794038ae2c1210557ad34857a4b6bdc515785d1da9e175b1da1e
#   2. Extract it anywhere.
#   3. ./tools/build-lame.ps1 -Source <path to the extracted lame-3.100 folder>

param(
    [Parameter(Mandatory = $true)]
    [string] $Source,

    [string] $Ndk = (Get-ChildItem "$env:ANDROID_HOME\ndk" -Directory | Sort-Object Name | Select-Object -Last 1).FullName,

    [int] $ApiLevel = 26
)

$ErrorActionPreference = "Stop"

$clang = Join-Path $Ndk "toolchains\llvm\prebuilt\windows-x86_64\bin\clang.exe"
if (-not (Test-Path $clang)) {
    throw "clang not found at $clang. Pass -Ndk with the NDK folder."
}

$sources = Get-ChildItem (Join-Path $Source "libmp3lame") -Filter *.c | Select-Object -ExpandProperty FullName
$output = Join-Path $PSScriptRoot "..\src\LinkAudioPlayer.App\Platforms\Android\lib"

# ABI folder name to clang target triple.
$targets = [ordered]@{
    "arm64-v8a"   = "aarch64-linux-android$ApiLevel"
    "armeabi-v7a" = "armv7a-linux-androideabi$ApiLevel"
    "x86_64"      = "x86_64-linux-android$ApiLevel"
    "x86"         = "i686-linux-android$ApiLevel"
}

foreach ($abi in $targets.Keys) {
    $directory = Join-Path $output $abi
    New-Item -ItemType Directory -Force $directory | Out-Null
    $library = Join-Path $directory "libmp3lame.so"

    # No config.h: the few switches LAME needs are passed directly.
    # max-page-size=16384 keeps the library loadable on 16 KB page devices, which
    # Android 15 introduced and Play requires for new uploads.
    & $clang "--target=$($targets[$abi])" -shared -fPIC -O2 -s `
        -DSTDC_HEADERS -DHAVE_LIMITS_H -DHAVE_STDINT_H -DHAVE_ERRNO_H -DHAVE_FCNTL_H `
        "-Dieee754_float32_t=float" `
        -Wno-everything `
        -I (Join-Path $Source "include") -I (Join-Path $Source "libmp3lame") `
        @sources -lm `
        "-Wl,-z,max-page-size=16384" "-Wl,-soname,libmp3lame.so" `
        -o $library

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for $abi"
    }

    "{0,-12} {1,8:N0} bytes" -f $abi, (Get-Item $library).Length
}
