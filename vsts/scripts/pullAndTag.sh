#!/bin/bash
# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license.
# --------------------------------------------------------------------------------------------

set -euo pipefail
# $1 > buildimage-acr.txt
# $2 > runtime-images-acr.txt
declare imagefilter="oryxdevmcr.azurecr.io/public/oryx"

function tagBuildImageForIntegrationTest() {
	local devbuildImageName="$1"
	local devbuildImageType="$2"
	local buildDefName="$BUILD_DEFINITIONNAME"
	local buildNumber="$RELEASE_TAG_NAME"
	
	# Always use specific build number based tag and then use the same tag to create a 'latest' tag and push it
	if [ -z "$devbuildImageType" ]; then
		buildImage=$devbuildImageName:$buildDefName.$buildNumber
		# Trim the build number tag and append the '':latest' to end of it
		newtag=$devbuildImageName:latest
	else
		buildImage=$devbuildImageName:$devbuildImageType-$buildDefName.$buildNumber
		newtag=$devbuildImageName:$devbuildImageType
	fi

	echo "Pulling the build image $buildImage ..."
 	docker pull "$buildImage" | sed 's/^/     /'

	echo
	echo "Tagging the source image with tag $newtag ..."
	
	docker tag "$buildImage" "$newtag" | sed 's/^/     /'
	echo
	echo -------------------------------------------------------------------------------
  
}

echo "Build image filter is set"

tagBuildImageForIntegrationTest "$imagefilter/build" ""
tagBuildImageForIntegrationTest "$imagefilter/build" "lts-versions" 
tagBuildImageForIntegrationTest "$imagefilter/build" "lts-versions-buster"
tagBuildImageForIntegrationTest "$imagefilter/build" "azfunc-jamstack"
tagBuildImageForIntegrationTest "$imagefilter/build" "azfunc-jamstack-buster" 
tagBuildImageForIntegrationTest "$imagefilter/build" "github-actions"
tagBuildImageForIntegrationTest "$imagefilter/build" "github-actions-buster"
tagBuildImageForIntegrationTest "$imagefilter/build" "vso-focal"
tagBuildImageForIntegrationTest "$imagefilter/build" "vso-slim"
tagBuildImageForIntegrationTest "$imagefilter/cli" ""
tagBuildImageForIntegrationTest "$imagefilter/cli-buster" ""
tagBuildImageForIntegrationTest "$imagefilter/pack" ""


# Extract language string from string (e.g extract 'python' from 'category=python')
if [ -n "$TESTINTEGRATIONCASEFILTER" ];then
	# For DB tests we want all the runtime images to be present at thae agent machine
	if [[ "$TESTINTEGRATIONCASEFILTER" != *db* ]];then
		imagefilter=$(echo $TESTINTEGRATIONCASEFILTER | cut -d'=' -f 2)
	fi
fi

# Always convert filter for runtime images to lower case
echo "Runtime image filter is set for "$imagefilter

while read sourceImage; do
  # Always use specific build number based tag and then use the same tag to create a version tag and push it
  if [[ "$sourceImage" != *:latest ]]; then
	if [[ "$sourceImage" == *"$imagefilter"* ]]; then
		echo "Pulling the runtime image $sourceImage ..."
		docker pull "$sourceImage" | sed 's/^/     /'

		# Trim the build number tag and append the version to end of it
		image="${sourceImage%:*}"
		echo
		echo "image $image"
		tagName="${sourceImage#$image:*}"
		echo "tagName $tagName"
		version="${tagName%%-*}"

		if [[ "$tagName" == *-fpm* ]]; then
			version="$version"-fpm
		fi

		echo "version $version"
		newtag="$image:$version"

		echo
		echo "Tagging the source image with tag $newtag ..."
		docker tag "$sourceImage" "$newtag" | sed 's/^/     /'
		echo
		echo -------------------------------------------------------------------------------
	fi
  fi
done <"$2"
