#!/bin/bash
set -e

# Since this file is expected to be 'sourced' from any script under any folder in this repo, 
# we cannot get the repo directory from this file itself and instead rely on the parent script
# which does the sourcing.
if [ -z "$REPO_DIR" ]; then
    "The variable 'REPO_DIR' cannot be empty. It should point to repo root."
    exit 1
fi

# sentinel variable to indicate that this file is loaded. Useful for files which source this.
declare -r _LOADED_COMMON_VARIABLES="true"

# VSTS environment variables
declare -r BUILD_NUMBER="$BUILD_BUILDNUMBER"
declare -r BUILD_CONFIGURATION="${BUILDCONFIGURATION:-Debug}"
declare -r RELEASE_TAG_NAME="${RELEASE_TAG_NAME:-$BUILD_NUMBER}"

declare -r BUILD_IMAGES_BUILD_CONTEXT_DIR="$REPO_DIR"
declare -r BUILD_IMAGES_DOCKERFILE="$REPO_DIR/images/build/Dockerfiles/Dockerfile"
declare -r BUILD_IMAGES_LTS_VERSIONS_DOCKERFILE="$REPO_DIR/images/build/Dockerfiles/ltsVersions.Dockerfile"
declare -r BUILD_IMAGES_LTS_VERSIONS_BUSTER_DOCKERFILE="$REPO_DIR/images/build/Dockerfiles/ltsVersions.buster.Dockerfile"
declare -r PACK_IMAGE_DOCKERFILE="$REPO_DIR/images/pack-builder/pack-runner.Dockerfile"
declare -r ORYXTESTS_BUILDIMAGE_DOCKERFILE="$REPO_DIR/tests/images/build/Dockerfile"
declare -r ORYXTESTS_LTS_VERSIONS_BUILDIMAGE_DOCKERFILE="$REPO_DIR/tests/images/build/ltsVersions.Dockerfile"
declare -r ORYXTESTS_LTS_VERSIONS_BUSTER_BUILDIMAGE_DOCKERFILE="$REPO_DIR/tests/images/build/ltsVersions.buster.Dockerfile"
declare -r ORYXTESTS_GITHUB_ACTIONS_BUILDIMAGE_DOCKERFILE="$REPO_DIR/tests/images/build/gitHubActions.Dockerfile"
declare -r RUNTIME_IMAGES_SRC_DIR="$REPO_DIR/images/runtime"
declare -r BUILD_IMAGES_CLI_DOCKERFILE="$REPO_DIR/images/build/Dockerfiles/cli.Dockerfile"
declare -r BUILD_IMAGES_AZ_FUNCS_JAMSTACK_DOCKERFILE="$REPO_DIR/images/build/Dockerfiles/azureFunctions.JamStack.Dockerfile"
declare -r BUILD_IMAGES_GITHUB_ACTIONS_DOCKERFILE="$REPO_DIR/images/build/Dockerfiles/gitHubActions.Dockerfile"
declare -r BUILD_IMAGES_VSO_SLIM_DOCKERFILE="$REPO_DIR/images/build/Dockerfiles/vso.slim.Dockerfile"
declare -r BUILD_IMAGES_VSO_FOCAL_DOCKERFILE="$REPO_DIR/images/build/Dockerfiles/vso.focal.Dockerfile"
declare -r BUILD_IMAGES_BUILDSCRIPTGENERATOR_DOCKERFILE="$REPO_DIR/images/build/Dockerfiles/buildScriptGenerator.Dockerfile"
declare -r BUILD_IMAGES_SUPPORT_FILES_DOCKERFILE="$REPO_DIR/images/build/Dockerfiles/supportFilesForBuildingBuildImages.Dockerfile"
declare -r BUILD_IMAGES_GITHUB_RUNNERS_BUILDPACKDEPS_STRETCH_DOCKERFILE="$REPO_DIR/images/build/Dockerfiles/gitHubRunners.BuildPackDepsStretch.Dockerfile"
declare -r BUILD_IMAGES_GITHUB_RUNNERS_BUILDPACKDEPS_FOCAL_DOCKERFILE="$REPO_DIR/images/build/Dockerfiles/gitHubRunners.BuildPackDepsFocal.Dockerfile"
declare -r BUILD_IMAGES_GITHUB_RUNNERS_BUILDPACKDEPS_BUSTER_DOCKERFILE="$REPO_DIR/images/build/Dockerfiles/gitHubRunners.BuildPackDepsBuster.Dockerfile"
declare -r RUNTIME_BASE_IMAGE_DOCKERFILE_PATH="$RUNTIME_IMAGES_SRC_DIR/commonbase/Dockerfile"
declare -r RUNTIME_BASE_IMAGE_NAME="oryx-run-base"
declare -r RUNTIME_BUSTER_BASE_IMAGE_NAME="oryx-run-base-buster"
declare -r SOURCES_SRC_DIR="$REPO_DIR/src"
declare -r TESTS_SRC_DIR="$REPO_DIR/tests"

declare -r ARTIFACTS_DIR="$REPO_DIR/artifacts"
declare -r BUILD_IMAGES_ARTIFACTS_FILE="$ARTIFACTS_DIR/images/build-images.txt"
declare -r BASE_IMAGES_ARTIFACTS_FILE_PREFIX="$ARTIFACTS_DIR/images"
declare -r RUNTIME_IMAGES_ARTIFACTS_FILE="$ARTIFACTS_DIR/images/runtime-images"
declare -r ACR_BUILD_IMAGES_ARTIFACTS_FILE="$ARTIFACTS_DIR/images/build-images-acr.txt"
declare -r ACR_RUNTIME_IMAGES_ARTIFACTS_FILE="$ARTIFACTS_DIR/images/runtime-images-acr"

declare -r PACK_IMAGE_NAME='pack'
declare -r PACK_STACK_BASE_IMAGE_NAME="pack-stack-base"
declare -r PACK_BUILDER_IMAGE_NAME="pack-builder"
declare -r PACK_TOOL_VERSION="0.18.1"
declare -r ORYXTESTS_BUILDIMAGE_REPO="oryxtests/build"

declare -r DEVBOX_BUILD_IMAGES_REPO="oryx/build"
declare -r DEVBOX_CLI_BUILD_IMAGE_REPO="oryx/cli"
declare -r DEVBOX_RUNTIME_IMAGES_REPO_PREFIX="oryx"

declare -r ACR_DEV_NAME="oryxdevmcr.azurecr.io"
declare -r ACR_PUBLIC_PREFIX="$ACR_DEV_NAME/public/oryx"
declare -r ACR_BUILD_IMAGES_REPO="$ACR_DEV_NAME/public/oryx/build"
declare -r ACR_CLI_BUILD_IMAGE_REPO="$ACR_DEV_NAME/public/oryx/cli"
declare -r ACR_RUNTIME_IMAGES_REPO="$ACR_PUBLIC_PREFIX"
declare -r ACR_PACK_IMAGE_REPO="$ACR_PUBLIC_PREFIX/$PACK_IMAGE_NAME"
declare -r ACR_AZURE_FUNCTIONS_JAMSTACK_IMAGE_NAME="$ACR_BUILD_IMAGES_REPO:azfunc-jamstack"
declare -r ACR_BUILD_LTS_VERSIONS_IMAGE_NAME="$ACR_BUILD_IMAGES_REPO:lts-versions"
declare -r ACR_BUILD_GITHUB_ACTIONS_IMAGE_NAME="$ACR_BUILD_IMAGES_REPO:github-actions"
declare -r ACR_BUILD_VSO_SLIM_IMAGE_NAME="$ACR_BUILD_IMAGES_REPO:vso-slim"
declare -r ACR_BUILD_VSO_FOCAL_IMAGE_NAME="$ACR_BUILD_IMAGES_REPO:vso-focal"

declare -r BASE_IMAGES_REPO="$ACR_DEV_NAME/public/oryx/base"

# Flag to add information to images through labels (example: build number, commit sha)
declare -r EMBED_BUILDCONTEXT_IN_IMAGES="${EMBEDBUILDCONTEXTINIMAGES:-false}"
declare -r GIT_COMMIT=$(git rev-parse HEAD)

declare -r DOCKER_SYSTEM_PRUNE="${ORYX_DOCKER_SYSTEM_PRUNE:-false}"

# If build_number has value that means we are building in build agent and not locally
if [ -n "$BUILD_NUMBER" ]; then
    declare -r AGENT_BUILD="true"
fi