// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

package main

type Configuration struct {
	NodeVersion                      string
	AppInsightsAgentExtensionVersion string
	EnableDynamicInstall             bool
	PreRunCommand                    string
}
