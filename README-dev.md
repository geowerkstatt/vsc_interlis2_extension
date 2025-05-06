# Welcome to your VS Code Extension

## What's in the folder

* This folder contains all of the files necessary for your extension.
* `package.json` - this is the manifest file in which you declare your language support and define the location of the grammar file that has been copied into your extension.
* `syntaxes/ili.tmLanguage.json` - this is the Text mate grammar file that is used for tokenization.
* `snippets/interlis2.json` - this is the file providing snippets for IntelliSense.
* `language-configuration.json` - this is the language configuration, defining the tokens that are used for comments and brackets.

## Get up and running straight away

* Make sure the language configuration settings in `language-configuration.json` are accurate.
* Press `F5` to open a new window with your extension loaded.
* Create a new file with a file name suffix matching your language.
* Verify that syntax highlighting works and that the language configuration settings are working.

## Make changes

* You can relaunch the extension from the debug toolbar after making changes to the files listed above.
* You can also reload (`Ctrl+R` or `Cmd+R` on Mac) the VS Code window with your extension to load your changes.

## Add more language features

* To add features such as intellisense, hovers and validators check out the VS Code extenders documentation at https://code.visualstudio.com/docs

## Install your extension

* To start using your extension with Visual Studio Code copy it into the `<user home>/.vscode/extensions` folder and restart Code.
* To share your extension with the world, read on https://code.visualstudio.com/docs about publishing an extension.

---

# Dev Notes
To setup the project for local development, you need to follow these steps
### 1. 🔑 Configure NuGet
In order to build the language server, access to our private packages is required. Create a file with the following template in the root directory and save it as `nuget.config`:

```
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="github" value="{NUGET_REPOSITORY_URL}" protocolVersion="3" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="{GH_USER_NAME}" />
      <add key="ClearTextPassword" value="{GH_PAT}" />
    </github>
  </packageSourceCredentials>
</configuration>
```
- replace `{NUGET_REPOSITORY_URL}` with our private github packages URL.
- replace `{GH_USER_NAME}` with your github user name (The user needs to have access to the NuGet repository).
- replace `{GH_PAT}` with a Personal Access Token holding at least a `read:packages` scope.

### 2. 📦 Bootstrap Everything
Open a terminal in the root directory of the project and run
```
npm install
```
### 3. 🕹️ Launch & Debug the Extension
To test changes or debug code:

1. Open the project as folder in VSC.
2. Navigate to the `Run and Debug` Tab or press `CTRL+ALT+D`.
3. In the dropdown, select `Extension for VS Code Desktop` and press `F5`.

This launches the extension in a new VSC Window, which we will call `Extension Host` for clarity.

### 4. (Optional) 🐞 Attach debugger to Language Server
If you want to debug changes in the Language Server, you need to attach a debugger _after_ the server has started. To accomplish this:

1. Open a `.ili` file in `Extension Host`.
2. This should start the language server automatically. Perform a sanity check by generating a markdown or diagramm via right clicking and selecting the appropriate option. If you get output and no errors, the server is running.
3. Navigate back to the original VSC window and open the `Run and Debug` Tab again.
4. In the dropdown, select `Attach to INTERLIS Language Server` and press `F5`.

The debugger is now attached.

### 5. 🔨 Build Extension
To test the extension as production build in a local setting, you can package the source code and install it directly in VSC.

1. In the project root, run `npm run package`
2. A new file has been created in root ending in `*.vsix`.
3. Install this vsix in any VSC Instance by opening the `Extensions` tab, clicking on the three dots, and selecting `Install from VSIX...`

Happy coding! 🚀
