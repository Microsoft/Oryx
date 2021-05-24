echo
echo "Using Node version:"
node --version
echo
{{ PackageInstallerVersionCommand }}

{{ if PackageRegistryUrl | IsNotBlank }}
echo
echo "Adding package registry to .npmrc: {{ PackageRegistryUrl }}"
echo "registry={{ PackageRegistryUrl }}" >> ~/.npmrc
echo
{{ end }}

{{ if NodeManifestFileName | IsNotBlank }}
MANIFEST_FILE={{ NodeManifestFileName }}

echo "Removing existing manifest file"
rm -f "$SOURCE_DIR/$MANIFEST_FILE"
{{ if NodeBuildProperties != empty }}
echo "Creating a manifest file..."
{{ for prop in NodeBuildProperties }}
echo "{{ prop.Key }}=\"{{ prop.Value }}\"" >> "$SOURCE_DIR/$MANIFEST_FILE"
{{ end }}
echo "Node Command Manifest file created."
{{ end }}
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
	{{ ProductionOnlyPackageInstallCommand }}

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
	{{ CustomBuildCommand }}
{{ else if CustomRunBuildCommand | IsNotBlank }}
	echo
	echo "Running '{{ PackageInstallCommand }}'..."
	echo
	{{ PackageInstallCommand }}
	echo
	{{ CustomRunBuildCommand }}
	echo
{{ else if LernaRunBuildCommand | IsNotBlank }}
	echo
	echo "Using Lerna version:"
	lerna --version
	echo
	echo
	echo "Running '{{ LernaInitCommand }} & {{ LernaBootstrapCommand }}':"
	{{ LernaInitCommand }}
	{{ LernaBootstrapCommand }}
	echo
	echo
	echo "Running '{{ LernaRunBuildCommand }}'..."
	echo
	{{ LernaRunBuildCommand }}
{{ else if LageRunBuildCommand | IsNotBlank }}
	echo
	echo "Running ' {{ InstallLageCommand }} ':"
	{{ InstallLageCommand }}
	echo
	echo
	echo "Running '{{ LageRunBuildCommand }}'..."
	echo
	{{ LageRunBuildCommand }}
{{ else }}
	echo
	echo "Running '{{ PackageInstallCommand }}'..."
	echo
	{{ PackageInstallCommand }}
	{{ if NpmRunBuildCommand | IsNotBlank }}
	echo
	echo "Running '{{ NpmRunBuildCommand }}'..."
	echo
	{{ NpmRunBuildCommand }}
	{{ end }}
	{{ if NpmRunBuildAzureCommand | IsNotBlank }}
	echo
	echo "Running '{{ NpmRunBuildAzureCommand }}'..."
	echo
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
echo
echo "Running 'npm pack'..."
echo
npm pack
{{ end }}

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
