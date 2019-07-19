#!/bin/bash
# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license.
# --------------------------------------------------------------------------------------------

set -e

declare -r REPO_DIR=$( cd $( dirname "$0" ) && cd .. && pwd )

# Load all variables
source $REPO_DIR/build/__variables.sh

echo
echo Building a build image for tests...
docker build -t $ORYXTESTS_BUILDIMAGE_REPO -f "$ORYXTESTS_BUILDIMAGE_DOCKERFILE" .

echo
dockerCleanupIfRequested
