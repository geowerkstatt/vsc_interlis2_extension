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
    nodesep: 80,
    ranksep: 80,
    marginx: 20,
    marginy: 20,
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
    const caluculatedHeight  = 42.5 + node.Data.Attributes.length * 17.5;
    return {
      ...node,
      targetPosition: isHorizontal ? "left" : "top",
      sourcePosition: isHorizontal ? "right" : "bottom",
      position: node.Position ?? {x: nodeWithPosition.x - nodeWidth / 2, y:  nodeWithPosition.y - caluculatedHeight / 2,
      },
    };
  });

  const layoutedEdges = edges.map((edge) => {
    const edgeInfo = dagreGraph.edge(edge.source, edge.target);
    return {
      ...edge,
      // type: ConnectionLineType.SmoothStep,
      sourcePosition: isHorizontal ? "right" : "bottom",
      targetPosition: isHorizontal ? "left" : "top",
      data: {
        points: edgeInfo.points,
      },
    };
  });
  return { layoutedNodes, layoutedEdges };
}

export function App() {
  const [nodes, setNodes, onNodesChange] = useNodesState([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState([]);
  const onConnect = useCallback((params) => setEdges((eds) => addEdge(params, eds)), [setEdges]);

  const handleNodesChange = useCallback(
    (changes) => {

      //Todo adapt style of changes
      console.log("onNodesChange:", changes);
      vscodeApi.postMessage({ type: "nodesChange", 
         changes: [{ className: changes[0].id, position: changes[0].position, }],
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
      const { layoutedNodes, layoutedEdges } = getLayoutedNodes(initialNodes, initialEdges);
      console.log(Edges);
      console.log(layoutedEdges);
      console.log(Nodes);
      console.log(layoutedNodes);
      setNodes(layoutedNodes);
      setEdges(layoutedEdges);
    });

    // Notify extension that webview is loaded
    vscodeApi.postMessage({ type: "webviewLoaded" });
  }, []);

  return (
    <div>
      <h2>INTERLIS Diagram (React)</h2>
      <div style={{ width: "100%", height: "90vh" }}>
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
        />
      </div>
    </div>
  );
}
