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

declare -a CommandList=('')

environmentPrefix="./venv"
echo
echo "Setting up Conda virtual environemnt..."
echo
START_TIME=$SECONDS
CondaEnvCreateCommand="conda env create --file $envFile --prefix $environmentPrefix --quiet"
CommandList=(${CommandList[*]}, $CondaEnvCreateCommand)
conda env create --file $envFile --prefix $environmentPrefix --quiet
ELAPSED_TIME=$(($SECONDS - $START_TIME))
echo "Done in $ELAPSED_TIME sec(s)."

{{ if HasRequirementsTxtFile }}
	echo
	echo "Activating environemnt..."
	CondaActivateCommand= "conda activate $environmentPrefix"
	CommandList=(${CommandList[*]}, $CondaActivateCommand)
	conda activate $environmentPrefix

	echo
	echo "Running pip install..."
	echo
	PipInstallCommand="pip install --no-cache-dir -r requirements.txt"
	CommandList=(${CommandList[*]}, $PipInstallCommand)
	pip install --no-cache-dir -r requirements.txt
{{ end }}

echo Commands=${CommandList[*]}
echo "PlatFormWithVersion=python {{ EnvironmentTemplatePythonVersion }}" >> "$COMMAND_MANIFEST_FILE"

ReadImageType=$(cat /opt/oryx/.imagetype)

if [ "$ReadImageType" = "vso-focal" ]
	echo $ReadImageType
	echo "BuildCommands=${CommandList[@]:1}" >> "$COMMAND_MANIFEST_FILE"
else
	echo "Not a vso image, so not writing build commands"
fi 
