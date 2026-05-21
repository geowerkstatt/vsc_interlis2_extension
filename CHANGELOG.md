the# Change Log
### vNext

* Markdown documentation:
  * Use UML notation `0..*` instead of `0..n` for unbounded multiplicities.
  * Drop `<b>` emphasis on top-level enumeration values.
  * Render abstract class names in italics.
  * New setting `interlis.documentation.abstractClassAttributes` (`separate` | `inline`); `inline` repeats abstract-parent attributes inside each subclass table.
  * New settings `interlis.documentation.attributeNameHeader`, `cardinalityHeader`, `typeHeader` to override the German default column headers.
  * Empty classes render `_keine Attribute in dieser Klasse_` instead of an empty table.
  * Render `FORMAT INTERLIS.XMLDate "..".."..."`, `ALL OF Domain`, and `SURFACE` / `AREA` / `POLYLINE` / `COORD` types instead of leaking the AST class name.
* Diagram preview:
  * Drop the `«class»` stereotype; use per-class `«abstract»` / `«structure»` / `«external»` instead.
  * Disambiguate classes that share a simple name across topics or models (label still shows the simple name).
  * Show abstract classes above their subclasses in TB orientation.
  * Render geometry types and formatted/all-of types instead of AST class names.
  * Hide the divider line of the empty operations compartment on class boxes (Mermaid still reserves the space; the line is no longer drawn).
* Stability: a syntactically invalid INTERLIS file no longer crashes the language server when generating markdown or the diagram; a clear failure message is shown instead.
* Security: escape model names, attribute names and configurable column headers in generated markdown/HTML and Mermaid diagrams so special characters can no longer break out of a table cell or diagram label; restrict the `geow.uml.color` meta-attribute to hex or plain color names.
* Syntax highlighting: highlight bare `0` and `*` consistently in numeric/cardinality positions.
* Documentation: README troubleshooting note for `spawn UNKNOWN` when the extension folder lacks execute permission.

### 0.4.2 - 2025-08-26

* Add _Format Document_ to language server.

### 0.4.1 - 2025-07-14

* Improved model imports in updated INTERLIS compiler.
* Show an error message when an imported model failed to compile.

### 0.4.0 - 2025-07-04

* Add _Go To Definition_ to language server:
  * Allows navigation to referenced symbols such as classes, structures and imported models.
  * Automatic download of imported models from INTERLIS model repository for navigation.
* Remove previous _Go To Implementation_ for imported models.
* Automatic preview of INTERLIS diagrams is disabled by default and can be enabled in the settings.

### 0.3.1 - 2025-06-05

* Improved model support in updated INTERLIS compiler.

### 0.3.0 - 2025-05-20

* Added visual preview for INTERLIS models. Preview opens automatically when an INTERLIS model is active in the editor, or can be opened manually by using the _Show INTERLIS Diagram View_ command.

### 0.2.1 - 2024-11-26

* Display struct attributes as nested tables in markdown documentation.

### 0.2.0 - 2024-11-11

* Add _Generate markdown documentation_ command using language server.

### 0.1.6 - 14.10.2022

* Fix _Go to Implementation_ not retrieving Models in subrepositories.

### 0.1.5 - 12.10.2022

* Rework TextMate grammar to separate included types from keywords
* Add _Go to Implementation_ functionality for VSCode desktop

### 0.1.4 - 18.10.2021

* Include syntax highlighting for block comments

### 0.1.3 - 15.07.2021

* Include snippets
* Higher resolution logo

### 0.1.2 - 21.05.2021

* Automate deployment via GitHub
* Fix typo in VIEW configuration

### 0.1.0 - 12.04.2021

* Added coloring of INTERLIS 2 Keywords.

