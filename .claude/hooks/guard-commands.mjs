// PreToolUse hook (Bash | PowerShell): blocks destructive or history-rewriting commands.
// Reads the hook payload from stdin; prints a deny decision as JSON when a rule matches.

const rules = [
  { pattern: /\bgit\s+push\b.*(\s--force\b|\s-f\b|\s--force-with-lease\b)/, reason: "Force-pushing rewrites shared history." },
  { pattern: /\bgit\s+reset\s+--hard\b/, reason: "git reset --hard discards uncommitted work." },
  { pattern: /\bgit\s+clean\s+-[a-z]*f/, reason: "git clean -f deletes untracked files permanently." },
  { pattern: /\bgit\s+(commit|push)\b.*--no-verify\b/, reason: "Skipping git hooks bypasses the quality gate." },
  { pattern: /\brm\s+-[a-z]*r[a-z]*f?\s+(\/|~|\.\.?)(\s|$)/, reason: "Recursive delete of a root, home or parent directory." },
  { pattern: /\bRemove-Item\b.*-Recurse\b.*\s(\\|\/|~|\.\.?|[A-Za-z]:\\?)(\s|$)/i, reason: "Recursive delete of a root, home or parent directory." },
  { pattern: /\bdotnet\s+ef\s+database\s+drop\b/, reason: "Dropping the database must be done by the developer." },
  { pattern: /\bdocker\s+(system|volume)\s+prune\b/, reason: "Pruning Docker removes data outside this project." },
];

let input = "";
process.stdin.setEncoding("utf8");
process.stdin.on("data", (chunk) => (input += chunk));
process.stdin.on("end", () => {
  let command = "";
  try {
    command = JSON.parse(input)?.tool_input?.command ?? "";
  } catch {
    process.exit(0); // Unparseable payload: do not block.
  }

  const hit = rules.find((rule) => rule.pattern.test(command));
  if (!hit) process.exit(0);

  process.stdout.write(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: "PreToolUse",
      permissionDecision: "deny",
      permissionDecisionReason: `Blocked by .claude/hooks/guard-commands.mjs: ${hit.reason} Ask the developer to run it manually if it is really intended.`,
    },
  }));
});
