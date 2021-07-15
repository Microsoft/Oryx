echo
dotnetCoreVersion=$(dotnet --version)
echo "Using .NET Core SDK Version: $dotnetCoreVersion"

{{ # .NET Core 1.1 based projects require restore to be run before publish }}
dotnet restore "{{ ProjectFile }}"

{{ if InstallBlazorWebAssemblyAOTWorkloadCommand | IsNotBlank }}
	echo
	echo "Running '{{ InstallBlazorWebAssemblyAOTWorkloadCommand }}'..."
	echo
	{{ InstallBlazorWebAssemblyAOTWorkloadCommand }}
{{ end }}

if [ "$SOURCE_DIR" == "$DESTINATION_DIR" ]
then
	dotnet publish "{{ ProjectFile }}" -c {{ Configuration }}
else
	echo
	echo "Publishing to directory $DESTINATION_DIR..."
	echo
	dotnet publish "{{ ProjectFile }}" -c {{ Configuration }} -o "$DESTINATION_DIR"
fi
