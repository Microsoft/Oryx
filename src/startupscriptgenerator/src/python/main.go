// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

package main

import (
	"common"
	"flag"
)

func main() {
	common.PrintVersionInfo()

	appPathPtr := flag.String("appPath", ".", "The path to the application folder, e.g. '/home/site/wwwroot/'.")

	manifestDirPtr := common.ManifestDirFlag

	userStartupCommandPtr := flag.String("userStartupCommand", "", "[Optional] Command that will be executed "+
		"to start the application up.")

	defaultAppFilePathPtr := flag.String("defaultApp", "", "[Optional] Path to a default file that will be "+
		"executed if the entrypoint is not found. Ex: '/opt/defaultsite'")

	defaultAppModulePtr := flag.String("defaultAppModule", "application:app", "Module of the default application,"+
		" e.g. 'application:app'.")

	defaultAppDebugModulePtr := flag.String("defaultAppDebugModule", "application.py", "Module to run if "+
		"running the app in debug mode, e.g. 'application.py start_dev_server'. Has no effect if -debugAdapter isn't used.")

	debugAdapterPtr := flag.String("debugAdapter", "", "Python debugger adapter. Currently, only 'ptvsd' is "+
		"supported.")

	debugPortPtr := flag.String("debugPort", "5678", "Port where the debugger will bind to. Has no effect if -debugAdapter isn't used.")

	debugWaitPtr := flag.Bool("debugWait", false, "Whether the debugger adapter should pause and wait for a "+
		"client connection before running the app.")

	virtualEnvNamePtr := flag.String("virtualEnvName", "", "Name of the virtual environment for the app")

	packagesFolderPtr := flag.String("packagedir", "", "Directory where the python packages were installed, if "+
		"no virtual environment was used.")

	bindPortPtr := flag.String("bindPort", "", "[Optional] Port where the application will bind to. Default is 80")

	outputPathPtr := flag.String("output", "run.sh", "Path to the script to be generated.")

	skipVirtualEnvExtraction := flag.Bool(
		"skipVirtualEnvExtraction",
		false,
		"Disables the extraction of the compressed virtual environment file. If used, some external tool will "+
			"have to extract it - otherwise the application might not work.")

	flag.Parse()

	logger := common.GetLogger("python.main")
	defer logger.Shutdown()
	logger.StartupScriptRequested()

	fullAppPath := common.GetValidatedFullPath(*appPathPtr)
	defaultAppFullPath := common.GetValidatedFullPath(*defaultAppFilePathPtr)

	buildManifest := common.GetBuildManifest(manifestDirPtr, fullAppPath)
	common.SetGlobalOperationID(buildManifest)

	entrypointGenerator := PythonStartupScriptGenerator{
		AppPath:                  fullAppPath,
		UserStartupCommand:       *userStartupCommandPtr,
		VirtualEnvName:           *virtualEnvNamePtr,
		BindPort:                 *bindPortPtr,
		DefaultAppPath:           defaultAppFullPath,
		DefaultAppModule:         *defaultAppModulePtr,
		DefaultAppDebugModule:    *defaultAppDebugModulePtr,
		DebugAdapter:             *debugAdapterPtr,
		DebugPort:                *debugPortPtr,
		DebugWait:                *debugWaitPtr,
		PackageDirectory:         *packagesFolderPtr,
		SkipVirtualEnvExtraction: *skipVirtualEnvExtraction,
		Manifest:                 buildManifest,
	}

	command := entrypointGenerator.GenerateEntrypointScript()
	common.WriteScript(*outputPathPtr, command)
}
