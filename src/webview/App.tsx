import React, { useEffect, useCallback } from "react";
import { ReactFlow, useNodesState, useEdgesState, addEdge } from "@xyflow/react";
import dagre from "dagre";
import { graphlib } from "dagre";
import "@xyflow/react/dist/style.css";
import ResizableNode from "./ResizableNode";

const nodeTypes = {
  ResizableNode,
};

const initialNodes = [
  // Topic_Kt_Codelisten
  {
    id: "SNP_Code",
    data: {
      label: (
        <div>
          <b>SNP_Code</b>
          <div>+1000..9999 Code</div>
          <div>+TEXT*12 Kuerzel</div>
          <div>+TEXT*80 Bezeichnung</div>
        </div>
      ),
    },
    type: "ResizableNode",
    position: { x: 0, y: 0 },
    style: { background: "green", color: "black", border: "2px solid black" },
  },

  // Topic_Transfermetadaten
  {
    id: "Stelle",
    data: {
      label: (
        <div>
          <b>Stelle</b>
          <div>+TEXT*80 Name</div>
          <div>+TEXT*80 Stelle_im_Web</div>
        </div>
      ),
    },
    type: "ResizableNode",
    position: { x: 300, y: 0 },
    style: { background: "white", color: "black", border: "2px solid black" },
  },
  {
    id: "Datenbestand",
    data: {
      label: (
        <div>
          <b>Datenbestand</b>
          <div>+TEXT*250 Gegenstand</div>
          <div>+INTERLIS.XMLDate Stand</div>
          <div>+INTERLIS.XMLDate Lieferdatum</div>
          <div>+TEXT*250 Bemerkung</div>
        </div>
      ),
    },
    type: "ResizableNode",

    position: { x: 600, y: 0 },
    style: { background: "white", color: "black", border: "2px solid black" },
  },

  // Topic_Rechtsvorschriften
  {
    id: "Dokument",
    data: {
      label: (
        <div>
          <b>Dokument</b>
          <div>+TEXT*80 Titel</div>
          <div>+TEXT*80 Text_im_Web</div>
          <div>+TEXT*250 Bemerkung</div>
        </div>
      ),
    },
    type: "ResizableNode",

    position: { x: 900, y: 0 },
    style: { background: "pink", color: "black", border: "2px solid black" },
  },

  // Topic_Sondernutzungsplaene
  {
    id: "SNP_Basis",
    data: {
      label: (
        <div>
          <b>SNP_Basis</b>
          <div>+TEXT*12 Objektnummer</div>
          <div>+TEXT*250 Gewaessername</div>
          <div>+Rechtsstatus Status</div>
          <div>+INTERLIS.XMLDate Datum_Entwurf</div>
          <div>+INTERLIS.XMLDate Datum_Auflage</div>
          <div>+INTERLIS.XMLDate Datum_Erlass</div>
          <div>+INTERLIS.XMLDate Datum_Rechtskraft</div>
          <div>+INTERLIS.XMLDate Datum_Aufhebung</div>
          <div>+TEXT*250 Bemerkung</div>
        </div>
      ),
    },
    type: "ResizableNode",

    position: { x: 0, y: 200 },
    style: { background: "lightyellow", color: "black", border: "2px solid black" },
  },
  {
    id: "SNP_Perimeter",
    data: {
      label: (
        <div>
          <b>SNP_Perimeter</b>
          <div>+SG_Basis_kt_V1_0_0.SGFlaeche2DKreisbogen Geometrie</div>
        </div>
      ),
    },
    type: "ResizableNode",

    position: { x: 300, y: 200 },
    style: { background: "lightyellow", color: "black", border: "2px solid black" },
  },
  {
    id: "SNP_Baulinie",
    data: {
      label: (
        <div>
          <b>SNP_Baulinie</b>
          <div>+WirkungBaulinie Wirkung</div>
          <div>+SG_Basis_kt_V1_0_0.SGLinie2DKreisbogen Geometrie</div>
        </div>
      ),
    },
    type: "ResizableNode",

    position: { x: 600, y: 200 },
    style: { background: "lightyellow", color: "black", border: "2px solid black" },
  },
  {
    id: "SNP_Flaeche",
    data: {
      label: (
        <div>
          <b>SNP_Flaeche</b>
          <div>+SG_Basis_kt_V1_0_0.SGFlaeche2DKreisbogen Geometrie</div>
        </div>
      ),
    },
    type: "ResizableNode",

    position: { x: 900, y: 200 },
    style: { background: "lightyellow", color: "black", border: "2px solid black" },
  },
  {
    id: "SNP_Linie",
    data: {
      label: (
        <div>
          <b>SNP_Linie</b>
          <div>+SG_Basis_kt_V1_0_0.SGLinie2DKreisbogen Geometrie</div>
        </div>
      ),
    },
    type: "ResizableNode",

    position: { x: 1200, y: 200 },
    style: { background: "lightyellow", color: "black", border: "2px solid black" },
  },
];

const initialEdges = [
  {
    id: "e1",
    source: "Datenbestand",
    target: "Stelle",
  },
  {
    id: "e2",
    source: "SNP_Basis",
    target: "Dokument",
  },
  {
    id: "e3",
    source: "SNP_Basis",
    target: "SNP_Code",
  },
  {
    id: "e4",
    source: "SNP_Perimeter",
    target: "SNP_Basis",
  },
  {
    id: "e5",
    source: "SNP_Baulinie",
    target: "SNP_Basis",
  },
  {
    id: "e6",
    source: "SNP_Flaeche",
    target: "SNP_Basis",
  },
  {
    id: "e7",
    source: "SNP_Linie",
    target: "SNP_Basis",
  },
];

const vscodeApi = (window as any).acquireVsCodeApi
  ? (window as any).acquireVsCodeApi()
  : {
      postMessage: () => {
        console.log("hi");
      },
    };
const dagreGraph = new graphlib.Graph();
dagreGraph.setDefaultEdgeLabel(() => ({}));
dagreGraph.setDefaultEdgeLabel(() => ({}));

const nodeWidth = 200;
const nodeHeight = 100;

function getLayoutedNodes(nodes, edges, direction = "LR") {
  dagreGraph.setGraph({ rankdir: direction });

  nodes.forEach((node) => {
    dagreGraph.setNode(node.id, { width: nodeWidth, height: nodeHeight });
  });

  edges.forEach((edge) => {
    dagreGraph.setEdge(edge.source, edge.target);
  });

  dagre.layout(dagreGraph);

  return nodes.map((node) => {
    const nodeWithPosition = dagreGraph.node(node.id);
    return {
      ...node,
      position: {
        x: nodeWithPosition.x - nodeWidth / 2,
        y: nodeWithPosition.y - nodeHeight / 2,
      },
    };
  });
}

export function App() {
  const [nodes, setNodes, onNodesChange] = useNodesState(initialNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(initialEdges);

  const onConnect = useCallback((params) => setEdges((eds) => addEdge(params, eds)), [setEdges]);

  useEffect(() => {
    // Listen for messages from the extension
    window.addEventListener("message", (event) => {
      const { type, text } = event.data;
      // Todo: Replace hardcoded nodes and edges with data received from the extension
      // if (type === "update")
      //   { setNodes(text);
      //     setEdges(text);
      //   };
    });

    // Notify extension that webview is loaded
    vscodeApi.postMessage({ type: "webviewLoaded" });

    const layoutedNodes = getLayoutedNodes(initialNodes, initialEdges);
    setNodes(layoutedNodes);
  }, []);

  return (
    <div>
      <h2>INTERLIS Diagram (React)</h2>
      <div style={{ width: "100%", height: "90vh" }}>
        <ReactFlow
          nodes={nodes}
          edges={edges}
          onNodesChange={onNodesChange}
          onEdgesChange={onEdgesChange}
          onConnect={onConnect}
          nodeTypes={nodeTypes}
          fitView
        />
      </div>
    </div>
  );
}
