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

- Object names like
  - `TOPIC`
  - `MODEL`
  - `CLASS`
- Data types like
  - `BOOLEAN`
  - `TEXT`
- Keywords like
  - `ASSOCIATION`
  - `ABSTRACT`
  - `EXTENDS`
- String patterns like
  - `{...}`

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

### Linter

The extension provides a linter. The default rule settings can be overwritten in the `.editorconfig`, e.g.:

```
dotnet_diagnostic.interlis.boolean-type.enabled = false
```

### Markdown documentation

The extension provides the command "Generate markdown documentation" to create markdown code from an INTERLIS 2 file describing the classes and their attributes. This command can be executed from the Command Palette (Default hotkey `Ctrl+Shift+P`) or the context menu of an open INTERLIS 2 file.

### Live Diagram View

Watch your ILI models come to life in real time. As you type, the diagram automatically updates so you can instantly see your model’s structure, spot inconsistencies, and keep everything aligned—no extra clicks required. Export the diagram as .SVG to embed in your webpages, or copy the mermaid code directly to your clipboard.
