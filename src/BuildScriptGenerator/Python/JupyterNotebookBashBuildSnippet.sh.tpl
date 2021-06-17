source $CONDA_SCRIPT

echo "Conda info:"
conda info

{{ if EnvironmentYmlFile | IsNotBlank }}
envFile="{{ EnvironmentYmlFile }}"
{{ else }}
envFile="/opt/oryx/conda/oryx.environment.yml"
envFileTemplate="/opt/oryx/conda/{{ EnvironmentTemplateFileName }}"
sed 's/PYTHON_VERSION/{{ EnvironmentTemplatePythonVersion }}/g' "$envFileTemplate" > $envFile
{{ end }}

echo "{{ NoteBookManifestFileName }}"

{{ if NoteBookManifestFileName | IsNotBlank }}
COMMAND_MANIFEST_FILE={{ NoteBookManifestFileName }}
{{ end }}

echo "PlatFormWithVersion=python {{ EnvironmentTemplatePythonVersion }}" >> "$COMMAND_MANIFEST_FILE"

declare -a CommandList=('')

environmentPrefix="./venv"
echo
echo "Setting up Conda virtual environemnt..."
echo
START_TIME=$SECONDS
CondaEnvCreateCommand="conda env create --file $envFile --prefix $environmentPrefix --quiet"
echo "BuildCommands=$CondaEnvCreateCommand" >> "$COMMAND_MANIFEST_FILE"
CommandList=(${CommandList[*]}, $CondaEnvCreateCommand)
conda env create --file $envFile --prefix $environmentPrefix --quiet
ELAPSED_TIME=$(($SECONDS - $START_TIME))
echo "Done in $ELAPSED_TIME sec(s)."

{{ if HasRequirementsTxtFile }}
	echo
	echo "Activating environemnt..."
	CondaActivateCommand= "conda activate $environmentPrefix"
	echo ", $CondaActivateCommand" >> "$COMMAND_MANIFEST_FILE"
	CommandList=(${CommandList[*]}, $CondaActivateCommand)
	conda activate $environmentPrefix

	echo
	echo "Running pip install..."
	echo
	PipInstallCommand="pip install --no-cache-dir -r requirements.txt"
	echo ", $PipInstallCommand" >> "$COMMAND_MANIFEST_FILE"
	CommandList=(${CommandList[*]}, $PipInstallCommand)
	pip install --no-cache-dir -r requirements.txt
{{ end }}

echo Commands=${CommandList[*]}

ReadImageType=$(cat /opt/oryx/.imagetype)

if [ "$ReadImageType" = "vso-focal" ]
	echo $ReadImageType
else
	rm "$COMMAND_MANIFEST_FILE"
fi 
