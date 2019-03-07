// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

package main

import (
	"startupscriptgenerator/common"
	"strings"
)

type PhpStartupScriptGenerator struct {
	SourcePath             string
}

func (gen *PhpStartupScriptGenerator) GenerateEntrypointScript() string {
	logger := common.GetLogger("python.scriptgenerator.GenerateEntrypointScript")
	defer logger.Shutdown()

	logger.LogInformation("Generating script for source at '%s'", gen.SourcePath)

	scriptBuilder := strings.Builder{}
	scriptBuilder.WriteString("#!/bin/sh\n")
	scriptBuilder.WriteString("\n# Enter the source directory to make sure the script runs where the user expects\n")
	scriptBuilder.WriteString("cd " + gen.SourcePath + "\n")
	scriptBuilder.WriteString("export APACHE_DOCUMENT_ROOT='" + gen.SourcePath + "'\n")
	scriptBuilder.WriteString("apache2-foreground\n")

	logger.LogProperties("Finalizing script", map[string]string{"root": gen.SourcePath})

	return scriptBuilder.String()
}
