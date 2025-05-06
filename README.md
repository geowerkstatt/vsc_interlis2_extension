# INTERLIS 2.4 language support

[![CI](https://github.com/GeoWerkstatt/vsc_interlis2_extension/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/GeoWerkstatt/vsc_interlis2_extension/actions/workflows/ci.yml)
[![Release](https://github.com/GeoWerkstatt/vsc_interlis2_extension/actions/workflows/release.yml/badge.svg)](https://github.com/GeoWerkstatt/vsc_interlis2_extension/actions/workflows/release.yml)
[![Visual Studio Marketplace Version](https://img.shields.io/visual-studio-marketplace/v/geowerkstatt.InterlisLanguageSupport)](https://marketplace.visualstudio.com/items?itemName=geowerkstatt.InterlisLanguageSupport)
[![License](https://img.shields.io/github/license/GeoWerkstatt/vsc_interlis2_extension)](https://github.com/GeoWerkstatt/vsc_interlis2_extension/blob/master/LICENSE.txt)


![image](https://user-images.githubusercontent.com/3465512/194811328-616104ab-5855-44e6-a5ec-0997d6403f59.png)


## Description
A Visual Studio Code extension providing INTERLIS 2.4 language support. The colors of the syntax highlighting are customizable using the colors of the current color theme by default. In addition, this extension provides snippets for commonly used blocks in INTERLIS 2 and a markdown documentation generator.

## Features
### Syntax Highlighting
The extension associates with `.ILI` files and applies coloring to the different elements in the file, for example:

* Object names like
    * `TOPIC`
    * `MODEL`
    * `CLASS`
* Data types like
    * `BOOLEAN`
    * `TEXT`
* Keywords like
    * `ASSOCIATION`
    * `ABSTRACT`
    * `EXTENDS`
* String patterns like
    * `{...}`

### Snippets
The extension provides interactive snippets for commonly used INTERLIS 2 blocks. To use the snippets start by typing the name of the block until VSC provides the correct option as suggestion. Select the snippet with the arrow keys and hit `ENTER` to insert. Navigate through the snippet and its options with `TAB`.

Supported snippets include: `MODEL`, `TOPIC`, `CLASS`, `STRUCTURE`, `ASSOCIATION` and `Role`

### File associations

To make VS Code treat other file extensions than the default `.ili` as INTERLIS2 files, add the following to the user settings:

```JSON
"files.associations": {
    "*.ili*": "INTERLIS2"
},
```
The example above associates extensions such as `.ili` with this extension.

### Markdown documentation
The extension provides the command "Generate markdown documentation" to create markdown code from an INTERLIS 2 file describing the classes and their attributes. This command can be executed from the Command Palette (Default hotkey `Ctrl+Shift+P`) or the context menu of an open INTERLIS 2 file.

### Live Diagram View
Watch your ILI models come to life in real time. As you type, the diagram automatically updates so you can instantly see your model’s structure, spot inconsistencies, and keep everything aligned—no extra clicks required. Export the diagram as .SVG to embed in your webpages, or copy the mermaid code directly to your clipboard.

---

## Dev Notes
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
