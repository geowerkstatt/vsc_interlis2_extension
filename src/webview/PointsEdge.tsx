import React from "react";
import { EdgeProps } from "reactflow";

export const PointsEdge: React.FC<EdgeProps> = ({ sourceX, sourceY, targetX, targetY, data }) => {
  // Use provided points as default
  let points = data?.points;
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
      { x: targetX, y: targetY },
    ];
  }

  const pointsString = points.map((p) => `${p.x},${p.y}`).join(" ");

  // Determine marker-end based on Mermaid symbol
  let markerEndId: string | undefined;
  let markerStartId: string | undefined;
  let strokeDasharray: string | undefined;

  switch (data.symbol) {
    case "<|--":
      markerStartId = "triangle";
      break;
    case "*--":
      markerStartId = "filledDiamond";
      break;
    case "o--":
      markerStartId = "whiteDiamond";
      break;
    case "--o":
      markerEndId = "whiteDiamond";
      break;
    case "o--o":
      markerEndId = "whiteDiamond";
      markerStartId = "whiteDiamond";
      break;
    case "<--":
      markerStartId = "arrow";
      break;
    case "--":
      // plain line
      break;
    case "..":
      strokeDasharray = "4 4";
      break;
    case "<|..":
      markerStartId = "triangle";
      strokeDasharray = "4 4";
      break;
    case "..>":
    case "-->":
    case "--|>":
      markerEndId = "triangle";
      break;
    default:
      markerEndId = "triangle";
  }

  return (
    <g>
      <polyline
        points={pointsString}
        fill="none"
        stroke="#fff"
        strokeWidth={2}
        markerEnd={markerEndId ? `url(#${markerEndId})` : undefined}
        markerStart={markerStartId ? `url(#${markerStartId})` : undefined}
        strokeDasharray={strokeDasharray}
      />
      <defs>
        <marker id="triangle" markerWidth="10" markerHeight="7" refX="10" refY="3.5" orient="auto">
          <polygon points="0 0, 10 3.5, 0 7" fill="#fff" />
        </marker>
        <marker id="filledDiamond" markerWidth="10" markerHeight="10" refX="10" refY="5" orient="auto">
          <polygon points="0,5 5,10 10,5 5,0" fill="black" strokeWidth={0.5} />
        </marker>
        <marker id="whiteDiamond" markerWidth="10" markerHeight="10" refX="10" refY="5" orient="auto">
          <polygon points="0,5 5,10 10,5 5,0" fill="#fff" stroke="#fff" strokeWidth={0.5} />
        </marker>
        <marker id="arrow" markerWidth="10" markerHeight="7" refX="10" refY="3.5" orient="auto">
          <path d="M 0 0 L 10 3.5 L 0 7 z" fill="#fff" />
        </marker>
      </defs>
    </g>
  );
};
