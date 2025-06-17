// eslint-disable-next-line @typescript-eslint/no-explicit-any
// Allow usage of 'any' type in this file
// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-nocheck
import React, { memo, useState } from "react";
import { Handle, Position, NodeResizer } from "@xyflow/react";
const ResizableNode = ({ id, data, style = {}, selected, onColorChange }) => {
  const [showColor, setShowColor] = useState(false);
  const handleDoubleClick = (e) => {
    e.stopPropagation();
    setShowColor(true);
  };
  const handleColorChange = (e) => {
    setShowColor(false);
    if (onColorChange) {
      onColorChange(id, e.target.value); // now updates background
    }
  };
  return (
    <>
      <NodeResizer minWidth={100} minHeight={30} isVisible={selected} />
      <Handle type="target" position={Position.Left} />
      <div
        style={{
          padding: 10,
          background: style.background || "transparent",
          border: style.border || "1px solid #ccc",
          color: style.color || "#000",
          borderRadius: 4,
          minWidth: 100,
          minHeight: 30,
          boxSizing: "border-box",
          width: "100%",
          height: "100%",
          cursor: "pointer",
        }}
        onDoubleClick={handleDoubleClick}
      >
        {data.label}
        {showColor && (
          <input
            type="color"
            autoFocus
            style={{ position: "absolute", zIndex: 10 }}
            defaultValue={style.background || "#FFFFFF"}
            onBlur={() => setShowColor(false)}
            onChange={handleColorChange}
          />
        )}
      </div>
      <Handle type="source" position={Position.Right} />
    </>
  );
};
export default memo(ResizableNode);
