#!/usr/bin/env bash
set -euo pipefail

# Publish the Android host with the .NET Android NativeAOT runtime.
# The Android project keeps PublishAot=false for ordinary IDE/build flows;
# this script enables NativeAOT only for an explicit release publish.

ProjectDir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# These defaults match the WSL toolchain used by the Avalonia 12 NativeAOT
# validation. Override them when the local WSL installation uses other paths.
AndroidSdkDirectory="${ANDROID_SDK_DIRECTORY:-$HOME/Android/Sdk}"
AndroidNdkDirectory="${ANDROID_NDK_DIRECTORY:-/mnt/d/ENV/_wsl/android-ndk-r27c}"
RuntimeIdentifier="${ANDROID_RUNTIME_IDENTIFIER:-android-arm64}"
ArtifactsPath="${NATIVEAOT_ARTIFACTS_PATH:-$HOME/avalime-nativeaot-artifacts}"

dotnet publish "$ProjectDir/Avalime.Android.csproj" \
  -c Release \
  -r "$RuntimeIdentifier" \
  --artifacts-path "$ArtifactsPath" \
  -p:UseMonoRuntime=false \
  -p:PublishAot=true \
  -p:PublishTrimmed=true \
  -p:AndroidSdkDirectory="$AndroidSdkDirectory" \
  -p:AndroidNdkDirectory="$AndroidNdkDirectory" \
  -p:AllowMissingPrunePackageData=true \
  -p:UseSharedCompilation=false \
  -nodeReuse:false

echo "NativeAOT publish completed: $ArtifactsPath"
