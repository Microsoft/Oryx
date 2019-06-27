#!/bin/bash
# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license.
# --------------------------------------------------------------------------------------------

set -e

declare -r REPO_DIR=$( cd $( dirname "$0" ) && cd .. && pwd )
declare -r buildBuildImagesScript="$REPO_DIR/build/buildBuildImages.sh"
declare -r testProjectName="Oryx.BuildImage.Tests"

# Load all variables
source $REPO_DIR/build/__variables.sh

if [ "$1" = "skipBuildingImages" ]
then
    echo
    echo "Skipping building build images as argument '$1' was passed..."
else
    echo
    echo "Invoking script '$buildBuildImagesScript'..."
    $buildBuildImagesScript "$@"
fi

testResult=$(dotnet test --filter "$MISSING_CATEGORY_FILTER" "$TESTS_SRC_DIR/$INTEGRATION_TEST_PROJECT/$INTEGRATION_TEST_PROJECT.csproj")

if [ "$testResult" == "No test matches the given testcase filter" ]; then 
    echo
    echo "All integration tests have category: No missing category tests found..."  
else 
    echo "$testResult"
fi

echo
echo "Building and running tests..."
cd "$TESTS_SRC_DIR/$testProjectName"
dotnet test --test-adapter-path:. --logger:"xunit;LogFilePath=$ARTIFACTS_DIR\testResults\\$testProjectName.xml" -c $BUILD_CONFIGURATION