#!/bin/bash
# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license.
# --------------------------------------------------------------------------------------------

# Perform case-insensitive comparison
matchesName() {
  local expectedName="$1"
  local providedName="$2"
  local result=
  shopt -s nocasematch
  [[ "$expectedName" == "$providedName" ]] && result=0 || result=1
  shopt -u nocasematch
   return $result
}

# Read the environment variables to see if a value for these variables have been set.
# If a variable was set as an environment variable AND as an argument to benv script, then the argument wins.
# Example:
#   export dotnet=1
#   source benv dotnet=3
#   dotnet --version (This should print version 3)
# What the following lines do? They rearrange the arguments that are passed to the script so that environment variables
# come before arguments passed to benv.
# Example: 
# Let us say there is an environment variable called NODE=10.14.3
# and benv was called like 'source benv node=12.16.3', then following lines make it like 
# it was called 'source benv NODE=10.14.3 node=12.16.3'. So here the last argument's value wins.
while read benvEnvironmentVariable; do
  set -- "$benvEnvironmentVariable" "$@"
done < <(set | grep -i '^dynamic_install_root_dir=')
while read benvEnvironmentVariable; do
  set -- "$benvEnvironmentVariable" "$@"
done < <(set | grep -i '^php=')
while read benvEnvironmentVariable; do
  set -- "$benvEnvironmentVariable" "$@"
done < <(set | grep -i '^python=')
while read benvEnvironmentVariable; do
  set -- "$benvEnvironmentVariable" "$@"
done < <(set | grep -i '^node=')
while read benvEnvironmentVariable; do
  set -- "$benvEnvironmentVariable" "$@"
done < <(set | grep -i '^npm=')
while read benvEnvironmentVariable; do
  set -- "$benvEnvironmentVariable" "$@"
done < <(set | grep -i '^dotnet=')
while read benvEnvironmentVariable; do
  set -- "$benvEnvironmentVariable" "$@"
done < <(set | grep -i '^hugo=')
while read benvEnvironmentVariable; do
  set -- "$benvEnvironmentVariable" "$@"
done < <(set | grep -i '^ruby=')
unset benvEnvironmentVariable # Remove all traces of this part of the script

# Oryx's paths come to the end of the PATH environment variable so that any user installed platform
# sdk versions can be picked up. Here we are trying to find the first occurrence of a path like '/opt/oryx'
# and inserting a more specific provided path after it.
# Example: (note that all Oryx related patlform paths come after the typical debian paths)
# /usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/opt/oryx:/opt/nodejs/6/bin:/opt/dotnet/sdks/2.2.401
updatePath() {
  if [ "$ORYX_PREFER_USER_INSTALLED_SDKS" == "true" ]
  then
    local replacingText=":/opt/oryx:$1:"
    local lookUpText=":\/opt\/oryx:"
    local currentPath="$PATH"
    local newPath=$(echo $currentPath | sed "0,/$lookUpText/ s##$replacingText#")
    export PATH="$newPath"
  else
    export PATH="$1:$PATH"
  fi
}

benv-getDynamicInstallRootDir() {
  # Iterate through arguments of the format "name=value"
  # and resolve each one, or exit if there is a failure.
  local explicitDynamicInstallRootDir=""
  while [[ $1 = *"="* ]]; do
    local name=$(echo $1 | sed 's/=.*$//')
    local value=$(echo $1 | sed 's/^.*=//')

    if matchesName "dynamic_install_root_dir" "$name"
    then
      explicitDynamicInstallRootDir="$value"
    fi
    shift
  done

  if [ -z "$explicitDynamicInstallRootDir" ]
  then
    echo "/tmp/oryx/platforms"
  else
    echo "$explicitDynamicInstallRootDir"
  fi
}

# NOTE: We handle .NET Core specially because there are 2 version types:
# SDK version and Runtime version
# For platforms other than dotnet, we look at a folder structure like '/opt/nodejs/10.14.1', but
# for dotnet, it would be '/opt/dotnet/runtimes/10.14.1'
# i.e Versioning of .NET Core is based on the runtime versions rather than sdk version
benv-showSupportedVersionsErrorInfo() {
  local userPlatformName="$1"
  local platformDirName="$2"
  local userSuppliedVersion="$3"
  local dynamicInstallRootDir="$4"
  local builtInInstallDir="/opt/$platformDirName"
  local dynamicInstallDir="$dynamicInstallRootDir/$platformDirName"

  if [ "$platformDirName" == "dotnet" ]; then
    builtInInstallDir="$builtInInstallDir/runtimes"
    dynamicInstallDir="$dynamicInstallDir/runtimes"
  fi

  echo >&2 benv: "$userPlatformName" version \'$userSuppliedVersion\' not found.
  if [ ! -d "$builtInInstallDir" ] && [ ! -d "$dynamicInstallDir" ]; then
    echo >&2 benv: Could not find any versions on disk.
  else
    echo >&2 benv: List of available versions:
  fi

  if [ -d "$builtInInstallDir" ]; then
    benv-versions >&2 "$builtInInstallDir"
  fi

  if [ -d "$dynamicInstallDir" ]; then
    benv-versions >&2 "$dynamicInstallDir"
  fi
}

benv-getPlatformDir() {
  local platformDirName="$1"
  local userSuppliedVersion="$2"
  local dynamicInstallRootDir="$3"
  local builtInInstallDir="/opt/$platformDirName"
  local dynamicInstallDir="$dynamicInstallRootDir/$platformDirName"
  
  if [ "$platformDirName" == "dotnet" ]; then
    builtInInstallDir="$builtInInstallDir/runtimes"
    dynamicInstallDir="$dynamicInstallDir/runtimes"
  fi

  if [ -d "$builtInInstallDir/$userSuppliedVersion" ]; then
    echo "$builtInInstallDir/$userSuppliedVersion"
  elif [ -d "$dynamicInstallDir/$userSuppliedVersion" ]; then
    echo "$dynamicInstallDir/$userSuppliedVersion"
  else
    echo "NotFound"
  fi
}

benv-versions() {
  local IFS=$' \r\n'
  local version
  for version in $(ls "$1"); do
    local link=$(readlink "$1/$version" || echo -n)
    if [ -z "$link" ]; then
      echo "  $version"
    else
      echo "  $version -> $link"
    fi
  done
}

# Since benv is going to be 'sourced', create a name which suggests it to not
# be used outside the context of benv script itself
_benvDynamicInstallRootDir=$(benv-getDynamicInstallRootDir "$@")

benv-resolve() {
  local name=$(echo $1 | sed 's/=.*$//')
  local value=$(echo $1 | sed 's/^.*=//')

  # Resolve node versions
  if matchesName "nodejs" "$name" \
    || matchesName "node" "$name" \
    || matchesName "node_version" "$name" \
    && [ "${value::1}" != "/" ]; then
    
    platformDir=$(benv-getPlatformDir "nodejs" "$value" "$_benvDynamicInstallRootDir")
    if [ "$platformDir" == "NotFound" ]; then
      benv-showSupportedVersionsErrorInfo "node" "nodejs" "$value" "$_benvDynamicInstallRootDir"
      return 1
    fi

    local DIR="$platformDir/bin"
    updatePath "$DIR"
    export node="$DIR/node"
    export npm="$DIR/npm"
    if [ -e "$DIR/npx" ]; then
      export npx="$DIR/npx"
    fi

    return 0
  fi

  # Resolve npm versions
  if matchesName "npm" "$name" || matchesName "npm_version" "$name" && [ "${value::1}" != "/" ]; then
    platformDir=$(benv-getPlatformDir "npm" "$value" "$_benvDynamicInstallRootDir")
    if [ "$platformDir" == "NotFound" ]; then
      benv-showSupportedVersionsErrorInfo "npm" "npm" "$value" "$_benvDynamicInstallRootDir"
      return 1
    fi

    local DIR="$platformDir"
    updatePath "$DIR"
    export npm="$DIR/npm"
    if [ -e "$DIR/npx" ]; then
      export npx="$DIR/npx"
    fi

    return 0
  fi

  # Resolve python versions
  if matchesName "python" "$name" || matchesName "python_version" "$name" && [ "${value::1}" != "/" ]; then
    platformDir=$(benv-getPlatformDir "python" "$value" "$_benvDynamicInstallRootDir")
    if [ "$platformDir" == "NotFound" ]; then
      benv-showSupportedVersionsErrorInfo "python" "python" "$value" "$_benvDynamicInstallRootDir"
      return 1
    fi

    IFS='.' read -ra SPLIT_VERSION <<< "$value"
    majorAndMinorParts="${SPLIT_VERSION[0]}.${SPLIT_VERSION[1]}"

    export LD_LIBRARY_PATH="$platformDir/lib:$LD_LIBRARY_PATH"

    local binDir="$platformDir/bin"
    updatePath "$binDir"
    export python="$binDir/python$majorAndMinorParts"
    export pip="$binDir/pip"
    if [ -e "$binDir/virtualenv" ]; then
      export virtualenv="$binDir/virtualenv"
    fi

    return 0
  fi

  # Resolve hugo versions
  if matchesName "hugo" "$name" || matchesName "hugo_version" "$name" && [ "${value::1}" != "/" ]; then
    platformDir=$(benv-getPlatformDir "hugo" "$value" "$_benvDynamicInstallRootDir")
    if [ "$platformDir" == "NotFound" ]; then
      benv-showSupportedVersionsErrorInfo "hugo" "hugo" "$value" "$_benvDynamicInstallRootDir"
      return 1
    fi

    local DIR="$platformDir"
    updatePath "$DIR"
    export hugo="$DIR/hugo"

    return 0
  fi

  # Resolve PHP versions
  if matchesName "php" "$name" || matchesName "php_version" "$name" && [ "${value::1}" != "/" ]; then
    platformDir=$(benv-getPlatformDir "php" "$value" "$_benvDynamicInstallRootDir")
    if [ "$platformDir" == "NotFound" ]; then
      benv-showSupportedVersionsErrorInfo "php" "php" "$value" "$_benvDynamicInstallRootDir"
      return 1
    fi

    export LD_LIBRARY_PATH="$platformDir/lib:$LD_LIBRARY_PATH"
    
    local DIR="$platformDir/bin"
    updatePath "$DIR"
    export php="$DIR/php"

    return 0
  fi

  # Resolve dotnet versions
  if matchesName "dotnet" "$name" || matchesName "dotnet_version" "$name" && [ "${value::1}" != "/" ]; then
    runtimeDir=$(benv-getPlatformDir "dotnet" "$value" "$_benvDynamicInstallRootDir")
    if [ "$runtimeDir" == "NotFound" ]; then
      benv-showSupportedVersionsErrorInfo "dotnet" "dotnet" "$value" "$_benvDynamicInstallRootDir"
      return 1
    fi

    local sdkVersion=$(cat "$runtimeDir/sdkVersion.txt" | tr -d '\r') 
    local SDK_DIR=$(cd "$runtimeDir/../../sdks/$sdkVersion" && pwd)
    toolsDir="$SDK_DIR/tools"
    if [ -d "$toolsDir" ]; then
      updatePath "$SDK_DIR/tools"
    fi

    updatePath "$SDK_DIR"
    export dotnet="$SDK_DIR/dotnet"
    
    return 0
  fi

  # Resolve ruby versions
  if matchesName "ruby" "$name" || matchesName "ruby_version" "$name" && [ "${value::1}" != "/" ]; then
    platformDir=$(benv-getPlatformDir "ruby" "$value" "$_benvDynamicInstallRootDir")
    if [ "$platformDir" == "NotFound" ]; then
      benv-showSupportedVersionsErrorInfo "ruby" "ruby" "$value" "$_benvDynamicInstallRootDir"
      return 1
    fi

    local binDIR="$platformDir/bin"
    updatePath "$binDIR"
    export ruby="$binDIR/ruby"
    export gem="$binDir/gem"
    return 0
  fi

  # Export other names without resolution
  eval export $name\=\'${value//\'/\'\\\'\'}\'
}

# Iterate through arguments of the format "name=value"
# and resolve each one, or exit if there is a failure.
while [[ $1 = *"="* ]]; do
  benv-resolve "$1" || if [ "$0" != "$BASH_SOURCE" ]; then
    # Remove all traces of this script prior to returning
    unset -f benv-resolve benv-versions;
    return 1;
  else
    exit 1;
  fi
  shift
done

if [ "$0" != "$BASH_SOURCE" ]; then
  # Remove all traces of this script prior to returning
  unset -f benv-resolve benv-versions
  if [ $# -gt 0 ]; then
    source "$@"
  fi
else
  if [ $# -eq 0 ]; then
    if [ $$ -eq 1 ]; then
      set -- bash
    else
      set -- env
    fi
  fi
  exec "$@"
fi