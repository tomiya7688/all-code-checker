import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const source = path.dirname(fileURLToPath(import.meta.url));
const assembly = process.argv[2];
if (!assembly) throw new Error("Output assembly path is required.");
const output = path.dirname(assembly);

const compiler = path.join(source, "node_modules", "typescript");
if (!fs.existsSync(path.join(compiler, "package.json"))) {
  throw new Error("TypeScript Compiler API is missing. Install helper dependencies first.");
}

const target = path.join(output, "typescript-runtime");
fs.mkdirSync(path.join(target, "node_modules"), { recursive: true });
const runtime = path.join(target, process.platform === "win32" ? "node.exe" : "node");
fs.copyFileSync(process.execPath, runtime);
if (process.platform !== "win32") fs.chmodSync(runtime, 0o755);
fs.copyFileSync(path.join(source, "analyze.cjs"), path.join(target, "analyze.cjs"));
fs.cpSync(compiler, path.join(target, "node_modules", "typescript"), { recursive: true, force: true });
