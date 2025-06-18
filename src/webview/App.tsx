import React, { useEffect, useCallback, useMemo, useState } from "react";
import { ReactFlow, useNodesState, useEdgesState, addEdge, ConnectionLineType } from "@xyflow/react";
import dagre from "dagre";
import { graphlib } from "dagre";
import "@xyflow/react/dist/style.css";
import ResizableNode from "./ResizableNode";
import { PointsEdge } from "./PointsEdge";

const edgeTypes = {
  PointsEdge,
};

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

function getBasicNodes(nodes) {
  return nodes.map((n) => ({
    ...n,
    id: n.Id,
    data: { ...n.Data, label: renderLabel(n.Data) },
    type: "ResizableNode",
    style: {
      background: n.Style?.Background ?? "#fff",
      color: n.Style?.Color ?? "#000",
      border: n.Style?.Border ?? "1px solid #000",
    },
  }));
}

function getBasicEdges(edges) {
  return edges.map((e) => ({
    ...e,
    id: e.Id,
    type: "PointsEdge",
    source: e.Source,
    target: e.Target,
  }));
}

function renderLabel(nodeData: { Title: string; Attributes: string[] }) {
  if (!nodeData) return;
  return (
    <div>
      <b>{nodeData.Title}</b>
      {nodeData.Attributes?.map((attr, idx) => (
        <div key={idx}>+{attr}</div>
      ))}
    </div>
  );
}

function getLayoutedNodes(nodes, edges, direction = "LR") {
  const isHorizontal = direction === "LR";
  dagreGraph.setGraph({
    rankdir: direction,
    nodesep: 50,
    ranksep: 50,
  });

  nodes.forEach((node) => {
    dagreGraph.setNode(node.id, { width: nodeWidth, height: 42.5 + node.Data.Attributes.length * 17.5 });
  });

  edges.forEach((edge) => {
    dagreGraph.setEdge(edge.source, edge.target);
  });

  dagre.layout(dagreGraph);

  const layoutedNodes = nodes.map((node) => {
    const nodeWithPosition = dagreGraph.node(node.id);
    const caluculatedHeight = 42.5 + node.Data.Attributes.length * 17.5;
    const targetPos = isHorizontal ? "left" : "top";
    const sourcePos = isHorizontal ? "right" : "bottom";
    return {
      ...node,
      targetPosition: targetPos,
      sourcePosition: sourcePos,
      position: node.Position ?? {
        x: nodeWithPosition.x - nodeWidth / 2,
        y: nodeWithPosition.y - caluculatedHeight / 2,
      },
    };
  });

  const layoutedEdges = edges.map((edge) => {
    const edgeInfo = dagreGraph.edge(edge.source, edge.target);
    return {
      ...edge,
      data: {
        points: edgeInfo.points,
        symbol: edge.Symbol ?? " --|>",
      },
    };
  });
  return { layoutedNodes, layoutedEdges };
}

export function App() {
  const [nodes, setNodes, onNodesChange] = useNodesState([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState([]);
  const [direction, setDirection] = useState("LR"); // State for layout direction
  const onConnect = useCallback((params) => setEdges((eds) => addEdge(params, eds)), [setEdges]);
  // Function to re-layout nodes with new direction
  const onLayoutChange = useCallback(
    (newDirection) => {
      setDirection(newDirection);
      setNodes((nds) => {
        const initialEdges = edges.map((e) => ({ ...(e as any) }));
        const { layoutedNodes, layoutedEdges } = getLayoutedNodes(nds, initialEdges, newDirection);
        setEdges(layoutedEdges);
        return layoutedNodes;
      });
    },
    [edges, setEdges, setNodes]
  );

  const handleNodesChange = useCallback(
    (changes) => {
      vscodeApi.postMessage({
        type: "nodesChange",
        changes: [{ className: changes[0].id, position: changes[0].position }],
      });
      onNodesChange(changes);
    },
    [onNodesChange]
  );

  // Add a handler to update node background color
  const handleNodeColorChange = (id, color) => {
    vscodeApi.postMessage({
      type: "colorChange",
      changes: [{ className: id, color: color }],
    });

    setNodes((nds) =>
      nds.map((node) =>
        node.id === id
          ? {
              ...node,
              style: { ...node.style, background: color },
            }
          : node
      )
    );
  };

  // Memoize nodeTypes to avoid React Flow warning and ensure color updates
  const nodeTypes = useMemo(
    () => ({
      ResizableNode: (nodeProps) => <ResizableNode {...nodeProps} onColorChange={handleNodeColorChange} />,
    }),
    [handleNodeColorChange]
  );
  useEffect(() => {
    // Listen for messages from the extension
    window.addEventListener("message", (event) => {
      const { type, text } = event.data;
      if (!text) return;
      const response = JSON.parse(text);
      const { Nodes, Edges } = response;
      const initialNodes = getBasicNodes(Nodes);
      const initialEdges = getBasicEdges(Edges);
      const { layoutedNodes, layoutedEdges } = getLayoutedNodes(initialNodes, initialEdges, direction);
      setNodes(layoutedNodes);
      setEdges(layoutedEdges);
    });

    // Notify extension that webview is loaded
    vscodeApi.postMessage({ type: "webviewLoaded" });
  }, [direction]);
  return (
    <div>
      <h2>UML Diagram</h2>
      <div style={{ position: "relative", width: "100%", height: "700px", padding: "10px" }}>
        <div
          style={{
            position: "absolute",
            right: "10px",
            top: "10px",
            zIndex: 10,
            display: "flex",
            gap: "10px",
            background: "white",
            padding: "5px",
            borderRadius: "5px",
            boxShadow: "0 1px 4px rgba(0, 0, 0, 0.2)",
          }}
        >
          <button
            onClick={() => onLayoutChange("LR")}
            style={{
              fontWeight: direction === "LR" ? "bold" : "normal",
              background: direction === "LR" ? "#e6f7ff" : "white",
              border: "1px solid #ccc",
              padding: "5px 10px",
              borderRadius: "4px",
              cursor: "pointer",
            }}
          >
            Horizontal
          </button>
          <button
            onClick={() => onLayoutChange("TB")}
            style={{
              fontWeight: direction === "TB" ? "bold" : "normal",
              background: direction === "TB" ? "#e6f7ff" : "white",
              border: "1px solid #ccc",
              padding: "5px 10px",
              borderRadius: "4px",
              cursor: "pointer",
            }}
          >
            Vertical
          </button>
        </div>
        <ReactFlow
          nodes={nodes}
          edges={edges}
          onNodesChange={handleNodesChange}
          onEdgesChange={onEdgesChange}
          onConnect={onConnect}
          nodeTypes={nodeTypes}
          edgeTypes={edgeTypes}
          connectionLineType={ConnectionLineType.SmoothStep}
          fitView
          fitViewOptions={{ padding: 0.2, includeHiddenNodes: false }}
        />
      </div>
    </div>
  );
}
