// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

package common

import (
	"common/consts"
	"fmt"
	"os"
	"path/filepath"

	"github.com/BurntSushi/toml"
)

type BuildManifest struct {
	Exists     bool
	Properties buildManifestProperties
}

type buildManifestProperties struct {
	StartupFileName           string
	ZipAllOutput              string
	OperationID               string
	VirtualEnvName            string
	PackageDir                string
	CompressedVirtualEnvFile  string
	StartupDllFileName        string
	InjectedAppInsights       string
	CompressedNodeModulesFile string
}

var _buildManifest BuildManifest
var _hasResult = false

func GetBuildManifest(manifestDir *string, fullAppPath string) BuildManifest {
	if _hasResult {
		return _buildManifest
	}

	manifestFileFullPath := getManifestFile(manifestDir, fullAppPath)
	_buildManifest = BuildManifest{}
	if FileExists(manifestFileFullPath) {
		fmt.Printf("Found build manifest file at '%s'. Deserializing it...\n", manifestFileFullPath)
		_buildManifest.Properties = deserializeBuildManifest(manifestFileFullPath)
		_buildManifest.Exists = true
	} else {
		fmt.Printf("Cound not find build manifest file at '%s'\n", manifestFileFullPath)
		_buildManifest.Properties = buildManifestProperties{}
		_buildManifest.Exists = false
	}

	_hasResult = true
	return _buildManifest
}

func getManifestFile(manifestDir *string, fullAppPath string) string {
	manifestFileFullPath := ""
	if *manifestDir == "" {
		manifestFileFullPath = filepath.Join(fullAppPath, consts.BuildManifestFileName)
	} else {
		providedPath := *manifestDir
		absPath, err := filepath.Abs(providedPath)
		if err != nil || !PathExists(absPath) {
			fmt.Printf(
				"Error: Provided manifest file directory path '%s' is not valid or does not exist.\n",
				providedPath)
			os.Exit(1)
		}

		manifestFileFullPath = filepath.Join(absPath, consts.BuildManifestFileName)
		if !FileExists(manifestFileFullPath) {
			fmt.Printf("Error: Could not file manifest file '%s' at '%s'.\n", consts.BuildManifestFileName, absPath)
			os.Exit(1)
		}
	}
	return manifestFileFullPath
}

func deserializeBuildManifest(manifestFile string) buildManifestProperties {
	var manifest buildManifestProperties
	if _, err := toml.DecodeFile(manifestFile, &manifest); err != nil {
		fmt.Printf(
			"Error occurred when trying to deserialize the manifest file '%s'. Error: '%s'.\n",
			manifestFile,
			err)
		os.Exit(1)
	}
	return manifest
}
