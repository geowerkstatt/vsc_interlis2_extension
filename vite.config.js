import { defineConfig } from "vite";

export default defineConfig({
  root: "src/webview",
  build: {
    outDir: "../../out/assets",
    emptyOutDir: true,
    rollupOptions: {
      input: "src/webview/index.tsx",
      output: {
        entryFileNames: "webview.js",
        format: "iife",
        name: "WebviewApp",
      },
    },
  },
});
