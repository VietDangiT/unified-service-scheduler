// PostToolUse hook (Edit | Write): applies .editorconfig whitespace formatting to the edited C# file.
// Uses `dotnet format whitespace --folder`, which needs no MSBuild load and runs in about a second.

import { spawnSync } from "node:child_process";
import path from "node:path";

let input = "";
process.stdin.setEncoding("utf8");
process.stdin.on("data", (chunk) => (input += chunk));
process.stdin.on("end", () => {
  let filePath = "";
  try {
    const payload = JSON.parse(input);
    filePath = payload?.tool_input?.file_path ?? payload?.tool_response?.filePath ?? "";
  } catch {
    process.exit(0);
  }
  if (!filePath.endsWith(".cs")) process.exit(0);

  const root = process.env.CLAUDE_PROJECT_DIR || process.cwd();
  const relative = path.relative(root, filePath);
  if (relative.startsWith("..")) process.exit(0); // Outside this repo.

  const result = spawnSync(
    "dotnet",
    ["format", "whitespace", root, "--folder", "--include", relative],
    { encoding: "utf8", shell: process.platform === "win32" },
  );

  if (result.status !== 0) {
    // Report but never block: formatting must not stop the agent from working.
    process.stderr.write(`dotnet format failed for ${relative}: ${result.stderr || result.stdout}`);
  }
  process.exit(0);
});
