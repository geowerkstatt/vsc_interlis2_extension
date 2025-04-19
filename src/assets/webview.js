const vscode = acquireVsCodeApi();

mermaid.initialize({ startOnLoad: false });

let renderTimer;

window.addEventListener("message", (event) => {
  if (event.data.type === "update") {
    clearTimeout(renderTimer);
    renderTimer = setTimeout(() => renderDiagram(event.data.text), 300);
  }
});

async function renderDiagram(mermaidCode) {
  const container = document.getElementById("mermaid-graph");
  try {
    const { svg } = await mermaid.render(`diagram-${Date.now()}`, mermaidCode);
    container.innerHTML = svg;
  } catch (err) {
    container.innerHTML = `<pre style="color:red">Error: ${err.message}</pre>`;
  }
}
