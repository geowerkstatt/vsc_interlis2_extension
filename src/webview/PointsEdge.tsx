import React from "react";
import { EdgeProps } from "reactflow";

export const PointsEdge: React.FC<EdgeProps> = ({ sourceX, sourceY, targetX, targetY, data }) => {
  // Use provided points as default
  let points = data?.points;

  // If points are not provided or the node positions have changed, use dynamic points
  if (
    !points ||
    points.length < 2 ||
    points[0].x !== sourceX ||
    points[0].y !== sourceY ||
    points[points.length - 1].x !== targetX ||
    points[points.length - 1].y !== targetY
  ) {
    points = [
      { x: sourceX, y: sourceY },
      // Optionally, add custom intermediate points here
      { x: targetX, y: targetY },
    ];
  }

  // Convert points to SVG polyline string
  const pointsString = points.map((p) => `${p.x},${p.y}`).join(" ");

  return (
    <g>
      <polyline points={pointsString} fill="none" stroke="#fff" strokeWidth={2} markerEnd="url(#arrowhead)" />
      <defs>
        <marker id="arrowhead" markerWidth="10" markerHeight="7" refX="10" refY="3.5" orient="auto">
          <polygon points="0 0, 10 3.5, 0 7" fill="#fff" />
        </marker>
      </defs>
    </g>
  );
};
