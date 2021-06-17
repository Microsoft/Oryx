{{ if NodeManifestFileName | IsNotBlank }}
COMMAND_MANIFEST_FILE={{ NodeManifestFileName }}

echo "Removing existing manifest file"
rm -f "$COMMAND_MANIFEST_FILE"
{{ if NodeBuildProperties != empty }}
echo "Creating a manifest file..."
{{ for prop in NodeBuildProperties }}
echo "{{ prop.Key }}={{ prop.Value }}" >> "$COMMAND_MANIFEST_FILE"
{{ end }}
echo "Node Command Manifest file created."
{{ end }}
{{ end }}

echo
echo "Using Node version:"
node --version
echo
echo "BuildCommands={{ PackageInstallerVersionCommand }}" >> "$COMMAND_MANIFEST_FILE"
{{ PackageInstallerVersionCommand }}

declare -a CommandList=('')

{{ if PackageRegistryUrl | IsNotBlank }}
echo
echo "Adding package registry to .npmrc: {{ PackageRegistryUrl }}"
echo "registry={{ PackageRegistryUrl }}" >> ~/.npmrc
echo
{{ end }}

zippedModulesFileName={{ CompressedNodeModulesFileName }}
allModulesDirName=".oryx_all_node_modules"
prodModulesDirName=".oryx_prod_node_modules"
PruneDevDependencies={{ PruneDevDependencies }}
HasProdDependencies={{ HasProdDependencies }}
HasDevDependencies={{ HasDevDependencies }}
packageDirName={{ PackageDirectory }}

# if node modules exist separately for dev & prod (like from an earlier build),
# rename the folders back appropriately for the current build
if [ -d "$allModulesDirName" ]
then
	echo
	echo "Found existing folder '$SOURCE_DIR/$allModulesDirName'."
	echo "Copying modules from '$SOURCE_DIR/$allModulesDirName' to '$SOURCE_DIR/node_modules'..."
	cd "$SOURCE_DIR"
	mkdir -p node_modules
	rsync -rcE --links "$allModulesDirName/" node_modules
fi

if [ "$PruneDevDependencies" == "true" ] && [ "$HasProdDependencies" == "true" ]
then
	# Delete existing prod modules folder so that we do not publish
	# any unused modules to final destination directory.
	if [ -d "$prodModulesDirName" ]; then
		echo "Found existing '$SOURCE_DIR/$prodModulesDirName'. Deleting it..."
		rm -rf "$prodModulesDirName"
	fi

	mkdir -p "$prodModulesDirName"
	cd "$prodModulesDirName"

	if [ -f "$SOURCE_DIR/package.json" ]; then
		cp -f "$SOURCE_DIR/package.json" .
	fi

	if [ -f "$SOURCE_DIR/package-lock.json" ]; then
		cp -f "$SOURCE_DIR/package-lock.json" .
	fi
	
	if [ -f "$SOURCE_DIR/.npmrc" ]; then
		cp -f "$SOURCE_DIR/.npmrc" .
	fi

	if [ -f "$SOURCE_DIR/yarn.lock" ]; then
		cp -f "$SOURCE_DIR/yarn.lock" .
	fi

	if [ -f "$SOURCE_DIR/.yarnrc" ]; then
		cp -f "$SOURCE_DIR/.yarnrc" .
	fi

	if [ -f "$SOURCE_DIR/.yarnrc.yml" ]; then
		cp -f "$SOURCE_DIR/.yarnrc.yml" .
	fi

	echo
	echo "Installing production dependencies in '$SOURCE_DIR/$prodModulesDirName'..."
	echo
	echo "Running '{{ ProductionOnlyPackageInstallCommand }}'..."
	echo
	echo ", {{ ProductionOnlyPackageInstallCommand }}" >> "$COMMAND_MANIFEST_FILE"
	{{ ProductionOnlyPackageInstallCommand }}
	
	CommandList=(${CommandList[*]}, '{{ ProductionOnlyPackageInstallCommand }}')

	if [ -d "node_modules" ]; then
		echo
		echo "Copying production dependencies from '$SOURCE_DIR/$prodModulesDirName' to '$SOURCE_DIR/node_modules'..."
		START_TIME=$SECONDS
		rsync -rcE --links "node_modules/" "$SOURCE_DIR/node_modules"
		ELAPSED_TIME=$(($SECONDS - $START_TIME))
		echo "Done in $ELAPSED_TIME sec(s)."
	fi
fi

cd "$SOURCE_DIR"

{{ if CustomBuildCommand | IsNotBlank }}
	echo
	echo "Running '{{ CustomBuildCommand }}'..."
	echo
	echo ", {{ CustomBuildCommand }}" >> "$COMMAND_MANIFEST_FILE"
	{{ CustomBuildCommand }}
	CommandList=(${CommandList[*]}, '{{ CustomBuildCommand }}')
{{ else if CustomRunBuildCommand | IsNotBlank }}
	echo
	echo "Running '{{ PackageInstallCommand }}'..."
	echo
	echo ", {{ PackageInstallCommand }}" >> "$COMMAND_MANIFEST_FILE"
	{{ PackageInstallCommand }}
	CommandList=(${CommandList[*]}, '{{ PackageInstallCommand }}')
	echo
	echo ", {{ CustomRunBuildCommand }}" >> "$COMMAND_MANIFEST_FILE"
	{{ CustomRunBuildCommand }}
	CommandList=(${CommandList[*]}, '{{ CustomRunBuildCommand }}')
	echo
{{ else if LernaRunBuildCommand | IsNotBlank }}
	echo
	echo "Using Lerna version:"
	lerna --version
	echo
	echo
	echo "Running '{{ LernaInitCommand }} & {{ LernaBootstrapCommand }}':"
	echo ", {{ LernaInitCommand }}', '{{ LernaBootstrapCommand }}" >> "$COMMAND_MANIFEST_FILE"
	{{ LernaInitCommand }}
	{{ LernaBootstrapCommand }}
	CommandList=(${CommandList[*]}, '{{ LernaInitCommand }}', '{{ LernaBootstrapCommand }}')
	echo
	echo
	echo "Running '{{ LernaRunBuildCommand }}'..."
	echo
	echo ", {{ LernaRunBuildCommand }}" >> "$COMMAND_MANIFEST_FILE"
	{{ LernaRunBuildCommand }}
	CommandList=(${CommandList[*]}, '{{ LernaRunBuildCommand }}')
{{ else if LageRunBuildCommand | IsNotBlank }}
	echo
	echo "Running ' {{ InstallLageCommand }} ':"
	echo ", {{ InstallLageCommand }}" >> "$COMMAND_MANIFEST_FILE"
	{{ InstallLageCommand }}
	CommandList=(${CommandList[*]}, '{{ InstallLageCommand }}')
	echo
	echo
	echo "Running '{{ LageRunBuildCommand }}'..."
	echo ", {{ LageRunBuildCommand }}" >> "$COMMAND_MANIFEST_FILE"
	echo
	{{ LageRunBuildCommand }}
	CommandList=(${CommandList[*]}, '{{ LageRunBuildCommand }}')
{{ else }}
	echo
	echo "Running '{{ PackageInstallCommand }}'..."
	echo
	echo ", {{ PackageInstallCommand }}" >> "$COMMAND_MANIFEST_FILE"
	{{ PackageInstallCommand }}
	CommandList=(${CommandList[*]}, '{{ PackageInstallCommand }}')
	{{ if NpmRunBuildCommand | IsNotBlank }}
	echo
	echo "Running '{{ NpmRunBuildCommand }}'..."
	echo ", {{ NpmRunBuildCommand }}" >> "$COMMAND_MANIFEST_FILE"
	echo
	{{ NpmRunBuildCommand }}
	CommandList=(${CommandList[*]}, '{{ NpmRunBuildCommand }}')
	{{ end }}
	{{ if NpmRunBuildAzureCommand | IsNotBlank }}
	echo
	echo "Running '{{ NpmRunBuildAzureCommand }}'..."
	echo ", {{ NpmRunBuildAzureCommand }}" >> "$COMMAND_MANIFEST_FILE"
	echo
	CommandList=(${CommandList[*]}, '{{ NpmRunBuildAzureCommand }}')
	{{ NpmRunBuildAzureCommand }}
	{{ end }}
{{ end }}

{{ if RunNpmPack }}
	{{ if PackageDirectory | IsNotBlank }}
	echo "Switching to package directory provided: '{{ PackageDirectory }}'..."
	if [ ! -d "$SOURCE_DIR/$packageDirName" ]; then
		echo "Package directory '$SOURCE_DIR/$packageDirName' does not exist." 1>&2
		exit 1
	fi
	cd "$SOURCE_DIR/$packageDirName"
	{{ end }}
echo
echo "Running custom packaging scripts that might exist..."
echo
npm run package || true
npm run prepublishOnly || true
echo ", npm run package || true, npm run prepublishOnly || true" >> "$COMMAND_MANIFEST_FILE"
CommandList=(${CommandList[*]}, 'npm run package || true', 'npm run prepublishOnly || true')
echo
echo "Running 'npm pack'..."
echo
echo ", npm pack" >> "$COMMAND_MANIFEST_FILE"
npm pack
CommandList=(${CommandList[*]}, 'npm pack')
{{ end }}

echo Commands=${CommandList[*]}

echo "${CommandList[@]:1}" 

ReadImageType=$(cat /opt/oryx/.imagetype)

if [ "$ReadImageType" = "vso-focal" ]
	echo $ReadImageType
else
	rm "$COMMAND_MANIFEST_FILE"
fi

cd "$SOURCE_DIR"

if [ "$PruneDevDependencies" == "true" ] && [ "$HasDevDependencies" == "true" ]
then
	if [ -d "node_modules" ]; then
		echo
		echo "Copy '$SOURCE_DIR/node_modules' with all dependencies to '$SOURCE_DIR/$allModulesDirName'..."
		rsync -rcE --links "node_modules/" "$allModulesDirName" --delete
	fi

	if [ "$HasProdDependencies" == "true" ] && [ -d "$prodModulesDirName/node_modules/" ]; then
		echo
		echo "Copying production dependencies from '$SOURCE_DIR/$prodModulesDirName/node_modules' to '$SOURCE_DIR/node_modules'..."
		rsync -rcE --links "$prodModulesDirName/node_modules/" node_modules --delete
	else
		rm -rf "node_modules/"
	fi
fi

{{ if CompressNodeModulesCommand | IsNotBlank }}
if [ "$SOURCE_DIR" != "$DESTINATION_DIR" ]
then
	if [ -f $zippedModulesFileName ]; then
		echo
		echo "File '$zippedModulesFileName' already exists under '$SOURCE_DIR'. Deleting it..."
		rm -f $zippedModulesFileName
	fi

	if [ -d node_modules ]
	then
		echo
		echo Zipping existing 'node_modules' folder...
		START_TIME=$SECONDS
		# Make the contents of the node_modules folder appear in the zip file, not the folder itself
		cd node_modules
		{{ CompressNodeModulesCommand }} ../$zippedModulesFileName .
		ELAPSED_TIME=$(($SECONDS - $START_TIME))
		echo "Done in $ELAPSED_TIME sec(s)."
	fi
fi
{{ end }}
