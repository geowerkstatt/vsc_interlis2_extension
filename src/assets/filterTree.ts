import { defaultFilters, DiagramFilters, FilterKindKey, KindFilter } from "./cytoscapeRenderer";

/**
 * The filter tree of the toolbar: one node per box kind with the parts of its boxes and its
 * arrows as leaves, then the frames. Built from this description so the markup, the labels and
 * the reading of the state stay in one place. A node's leaves are disabled while the node itself
 * is switched off, since they have no effect then.
 */

/** A leaf or node of the tree; `set` writes the checkbox state into the filters, `get` reads it back. */
interface FilterEntry {
  label: string;
  title: string;
  get(filters: DiagramFilters): boolean;
  set(filters: DiagramFilters, value: boolean): void;
  children?: FilterEntry[];
}

function kindEntry(kind: FilterKindKey, label: string, title: string): FilterEntry {
  const aspect = (name: keyof KindFilter, text: string, hint: string, children?: FilterEntry[]): FilterEntry => ({
    label: text,
    title: hint,
    get: (filters) => filters.kinds[kind][name],
    set: (filters, value) => {
      filters.kinds[kind][name] = value;
    },
    children,
  });
  const attributes =
    kind === "domain"
      ? aspect("attributes", "Definition", "The type the domain defines")
      : aspect("attributes", "Attributes", "The attribute compartment", [
          aspect("cardinality", "Cardinality", "The multiplicity of an attribute, e.g. [0..*]"),
          aspect("types", "Type", "The type of an attribute"),
        ]);
  const children = [
    attributes,
    aspect("constraints", "Constraints", "The constraint compartment"),
    aspect("connections", "Connections", `Arrows touching a ${label.toLowerCase().replace(/s$/, "")}`),
  ];
  return {
    label,
    title,
    get: (filters) => filters.kinds[kind].visible,
    set: (filters, value) => {
      filters.kinds[kind].visible = value;
    },
    children,
  };
}

const TREE: FilterEntry[] = [
  kindEntry("class", "Classes", "Class boxes"),
  kindEntry("structure", "Structures", "Structure boxes"),
  kindEntry("view", "Views", "View boxes"),
  kindEntry("association", "Associations", "Association boxes and lines, with the role arrows of the boxes"),
  kindEntry("domain", "Domains", "Domain boxes"),
  {
    label: "Imported models",
    title: "Models read from other files, with everything they contain",
    get: (filters) => filters.importedModels,
    set: (filters, value) => {
      filters.importedModels = value;
    },
    children: [
      {
        label: "Indirectly referenced",
        title:
          "Boxes of imported models that no arrow connects to a box of this file, and the models and topics holding only such boxes; switch off to keep just what this file references directly",
        get: (filters) => filters.indirect,
        set: (filters, value) => {
          filters.indirect = value;
        },
      },
    ],
  },
  {
    label: "Model connections",
    title: "Arrows between models for IMPORTS",
    get: (filters) => filters.imports,
    set: (filters, value) => {
      filters.imports = value;
    },
  },
  {
    label: "Topic connections",
    title: "Arrows between topics for EXTENDS",
    get: (filters) => filters.topicExtensions,
    set: (filters, value) => {
      filters.topicExtensions = value;
    },
  },
];

interface Bound {
  entry: FilterEntry;
  input: HTMLInputElement;
  /** The container of the entry's children, disabled while the entry is switched off. */
  children?: HTMLElement;
}

export interface FilterTree {
  /** Returns to the default filters and reports the change. */
  reset(): void;
}

/**
 * Renders the tree into `container` and reports every change with the complete filter state.
 * Every row is a switch drawn as an eye, with a checkbox underneath for the state and the
 * keyboard. The root nodes start collapsed and only their twisty opens them, so a click on a
 * switch changes the filter and nothing else; the nodes inside are open from the start.
 */
export function createFilterTree(container: HTMLElement, onChange: (filters: DiagramFilters) => void): FilterTree {
  let filters = defaultFilters();
  const bound: Bound[] = [];

  function checkbox(entry: FilterEntry): HTMLLabelElement {
    const label = document.createElement("label");
    label.className = "filter-label";
    label.title = entry.title;
    const input = document.createElement("input");
    input.type = "checkbox";
    input.checked = entry.get(filters);
    label.append(input, icon("icon-eye", "eye-on"), icon("icon-eye-closed", "eye-off"), ` ${entry.label}`);
    bound.push({ entry, input });
    return label;
  }

  /** An icon of the sprite of the page. */
  function icon(id: string, className: string): SVGSVGElement {
    const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    svg.setAttribute("class", className);
    const use = document.createElementNS("http://www.w3.org/2000/svg", "use");
    use.setAttribute("href", `#${id}`);
    svg.appendChild(use);
    return svg;
  }

  /** The chevron that opens a node; a leaf gets an empty one, so the switches of a level line up. */
  function twisty(leaf: boolean): HTMLSpanElement {
    const span = document.createElement("span");
    span.className = "twisty";
    if (!leaf) span.appendChild(icon("icon-chevron", "chevron"));
    return span;
  }

  function build(entries: FilterEntry[], parent: HTMLElement, depth: number): void {
    for (const entry of entries) {
      if (!entry.children) {
        const row = document.createElement("div");
        row.className = "filter-leaf";
        row.append(twisty(true), checkbox(entry));
        parent.appendChild(row);
        continue;
      }
      const details = document.createElement("details");
      details.className = "filter-node";
      details.open = depth > 0;
      const summary = document.createElement("summary");
      summary.append(twisty(false), checkbox(entry));
      const node = bound[bound.length - 1]; // the entry just bound, before its children are
      const children = document.createElement("div");
      children.className = "filter-children";
      build(entry.children, children, depth + 1);
      node.children = children;
      details.append(summary, children);
      parent.appendChild(details);
    }
  }

  function updateDisabled(): void {
    for (const { input, children } of bound) {
      if (!children) continue;
      const disabled = !input.checked || input.disabled;
      children.querySelectorAll("input").forEach((child) => {
        child.disabled = disabled;
      });
    }
  }

  function report(): void {
    updateDisabled();
    onChange(structuredClone(filters)); // a copy: the tree keeps changing its own
  }

  build(TREE, container, 0);
  container.addEventListener("change", (event) => {
    const target = event.target as HTMLInputElement;
    const entry = bound.find((item) => item.input === target);
    if (!entry) return;
    entry.entry.set(filters, target.checked);
    report();
  });
  updateDisabled();

  return {
    reset() {
      filters = defaultFilters();
      for (const { entry, input } of bound) {
        input.checked = entry.get(filters);
      }
      report();
    },
  };
}
