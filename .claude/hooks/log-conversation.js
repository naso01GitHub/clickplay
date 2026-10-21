#!/usr/bin/env node
/**
 * Claude Code hook: logs the conversation into conversations/ (gitignored).
 *
 * - UserPromptSubmit: appends the prompt to conversations/claude-prompts.log
 * - Stop / SessionEnd: rebuilds a readable Markdown transcript from transcript_path
 *   into conversations/claude-<date>-<session>.md and conversations/claude-latest.md
 *
 * Never blocks Claude Code: every error is swallowed and the script exits 0.
 */

const fs = require("fs");
const path = require("path");

const projectDir = path.resolve(__dirname, "..", "..");
const outDir = path.join(projectDir, "conversations");

/**
 * @brief Reads the hook input JSON that Claude Code writes to stdin.
 * @return {Promise<string>} The stdin text without a BOM; empty on read error.
 *
 * @code
 * const raw = await readStdin(); // '{"hook_event_name":"Stop", ...}'
 * @endcode
 */
function readStdin() {
  return new Promise((resolve) => {
    const chunks = [];
    process.stdin.on("data", (c) => chunks.push(Buffer.from(c)));
    process.stdin.on("end", () => resolve(Buffer.concat(chunks).toString("utf8").replace(/^﻿/, "")));
    process.stdin.on("error", () => resolve(""));
  });
}

/**
 * @brief Formats a local date-time as "yyyy-MM-dd HH:mm:ss".
 * @param {string|number|Date} [date] Time to format; now when omitted.
 * @return {string} The formatted local time.
 *
 * @code
 * stamp("2026-10-21T18:38:19Z"); // "2026-10-21 21:38:19" in Sofia time
 * @endcode
 */
function stamp(date) {
  const d = date ? new Date(date) : new Date();
  const pad = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
}

/**
 * @brief Removes harness-injected blocks (system reminders, IDE context, command tags) so only what the user typed remains.
 * @param {string} text Raw user message text from the transcript.
 * @return {string} Cleaned, trimmed text; empty when nothing typed remains.
 *
 * @code
 * cleanUserText("<ide_opened_file>…</ide_opened_file>compile it"); // "compile it"
 * @endcode
 */
function cleanUserText(text) {
  return String(text || "")
    .replace(/<system-reminder>[\s\S]*?<\/system-reminder>/g, "")
    .replace(/<ide_[a-z_]+>[\s\S]*?<\/ide_[a-z_]+>/g, "")
    .replace(/<command-message>[\s\S]*?<\/command-message>/g, "")
    .replace(/<command-name>([\s\S]*?)<\/command-name>/g, "$1")
    .trim();
}

/**
 * @brief Turns one transcript message content into readable text.
 *        Keeps text blocks; shows assistant tool calls as one "🔧 Tool: hint" line; skips thinking and tool results.
 * @param {string|Array<object>} content Message content: a string or an array of content blocks.
 * @param {string} role "user" or "assistant".
 * @return {string} Markdown text; empty when the message has nothing to show.
 *
 * @code
 * textOf([{ type: "text", text: "Hi" }, { type: "tool_use", name: "Bash", input: { description: "Run tests" } }], "assistant");
 * // "Hi\n\n> 🔧 Bash: Run tests"
 * @endcode
 */
function textOf(content, role) {
  if (typeof content === "string") {
    return role === "user" ? cleanUserText(content) : content.trim();
  }

  if (!Array.isArray(content)) {
    return "";
  }

  const parts = [];
  for (const block of content) {
    if (!block || typeof block !== "object") continue;
    if (block.type === "text" && block.text) {
      parts.push(role === "user" ? cleanUserText(block.text) : block.text.trim());
    } else if (block.type === "tool_use" && role === "assistant") {
      const input = block.input || {};
      const hint = input.description || input.file_path || input.command || input.pattern || input.query || "";
      parts.push(`> 🔧 ${block.name}${hint ? `: ${String(hint).split("\n")[0].slice(0, 160)}` : ""}`);
    }
  }

  return parts.filter(Boolean).join("\n\n");
}

/**
 * @brief Builds a Markdown transcript from a Claude Code session JSONL file.
 *        Skips meta and sidechain (subagent) entries; adds a "## User/Claude — time" heading when the speaker changes.
 * @param {string} transcriptPath Path to the session .jsonl file (transcript_path from the hook input).
 * @param {string} sessionId Session id for the title.
 * @return {string} The Markdown document.
 * @throws {Error} When the transcript file cannot be read.
 *
 * @code
 * const md = renderTranscript(input.transcript_path, input.session_id);
 * @endcode
 */
function renderTranscript(transcriptPath, sessionId) {
  const lines = fs.readFileSync(transcriptPath, "utf8").split("\n").filter(Boolean);
  const out = [`# Claude Code conversation ${sessionId}`, ""];
  let lastRole = "";
  for (const line of lines) {
    let entry;
    try {
      entry = JSON.parse(line);
    } catch {
      continue;
    }

    if ((entry.type !== "user" && entry.type !== "assistant") || entry.isMeta || entry.isSidechain || !entry.message) {
      continue;
    }

    const role = entry.message.role || entry.type;
    const text = textOf(entry.message.content, role);
    if (!text) continue;

    if (role !== lastRole) {
      out.push(`## ${role === "user" ? "User" : "Claude"} — ${stamp(entry.timestamp)}`, "");
      lastRole = role;
    }

    out.push(text, "");
  }

  return out.join("\n");
}

/**
 * @brief Hook entry point: on UserPromptSubmit appends the prompt to claude-prompts.log;
 *        on Stop or SessionEnd rewrites claude-<date>-<session>.md and claude-latest.md.
 * @return {Promise<void>} Resolves when the log files are written; never rejects to the caller.
 *
 * @code
 * // echo '{"hook_event_name":"UserPromptSubmit","session_id":"abc","prompt":"hi"}' | node log-conversation.js
 * main().finally(() => process.exit(0));
 * @endcode
 */
async function main() {
  let input = {};
  try {
    const raw = await readStdin();
    input = raw.trim() ? JSON.parse(raw) : {};
  } catch {
    input = {};
  }

  fs.mkdirSync(outDir, { recursive: true });
  const event = input.hook_event_name || "";
  const sessionId = String(input.session_id || "unknown");

  if (event === "UserPromptSubmit") {
    const prompt = String(input.prompt || "").trim();
    fs.appendFileSync(
      path.join(outDir, "claude-prompts.log"),
      `[${stamp()}] [${sessionId.slice(0, 8)}]\n${prompt}\n\n`,
      "utf8");
    return;
  }

  if ((event === "Stop" || event === "SessionEnd") && input.transcript_path && fs.existsSync(input.transcript_path)) {
    const markdown = renderTranscript(input.transcript_path, sessionId);
    const day = stamp().slice(0, 10);
    fs.writeFileSync(path.join(outDir, `claude-${day}-${sessionId.slice(0, 8)}.md`), markdown, "utf8");
    fs.writeFileSync(path.join(outDir, "claude-latest.md"), markdown, "utf8");
  }
}

main()
  .catch(() => {})
  .finally(() => process.exit(0));
