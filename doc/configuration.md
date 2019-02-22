# Oryx configuration

Oryx provides configuration options through environment variables so that you
can apply minor adjustments and still utilize the automatic build process. The following variables are supported today:

> NOTE: In Azure Web Apps, these variables are set as App Settings [App Settings][].

Setting name                     | Description                                                    | Example
---------------------------------|----------------------------------------------------------------|------------
DISABLE\_COLLECTSTATIC           | Disable running `collecstatic` when building Python applications. Valid values are `true` or `false`. |
PRE\_BUILD\_SCRIPT\_PATH         | repo-relative path to a shell script to be run before build    | scripts/prebuild.sh
POST\_BUILD\_SCRIPT\_PATH        | repo-relative path to a shell script to be run after build     | scripts/postbuild.sh
PROJECT                          | repo-relative path to directory with `.csproj` file for build  | src/WebApp1/WebApp1.csproj
DISABLE\_DOTNETCORE\_BUILD       | if `true`, the system won't attempt to detect or build any .NET Core code in the repo. |
DISABLE\_PYTHON\_BUILD           | if `true`, the system won't attempt to detect or build any Python code in the repo. |
DISABLE\_NODEJS\_BUILD           | if `true`, the system won't attempt to detect or build any NodeJS code in the repo. |
ENABLE\_MULTIPLATFORM\_BUILD     | if `true`, the system will automatically detect the platforms used in the repo and build them. If needed, each platform can be individually disabled using its own flag described above. |

# Azure Web Apps configuration

Within Azure Web Apps, Oryx's environment variables are set via [App
Settings][].

List and modify these App Settings with the [az CLI][] using the following
commands:

```bash
app_group=your-group
app_name=your-app

# list current settings
az webapp config appsettings list \
  --resource-group $app_group --name $app_name \
  --output table

# replace current settings
az webapp config appsettings set \
    --resource-group $app_group --name $app_name \
    --settings \
      "settingA=${settingA}" \
      "settingB=${settingB}"
```

App Service adds the following settings that govern build:

Setting name                     | Description                                                    | Example
---------------------------------|----------------------------------------------------------------|------------
ENABLE\_ORYX\_BUILD              | if `true`, use the Oryx build system instead of the legacy Kudu system | 
COMMAND                          | provide an alternate build-and-run script. Bypasses automatic build completely. | "repo/path/to/script.sh"
SCM\_DO\_BUILD\_DURING\_DEPLOYMENT` | if `false`, bypass automatic build |

## Startup file

Within App Service, to explicitly specify a start script use the
`--startup-file` parameter of `az webapp create ...` or `az webapp config set
...`.

[App Settings]: https://docs.microsoft.com/en-us/azure/app-service/web-sites-configure#app-settings
[az CLI]: https://github.com/Azure/azure-cli
