#!/usr/bin/env node
/**
 * Saves Agent chat turns into conversations/ inside the project folder.
 *
 * Events:
 * - beforeSubmitPrompt: append user message
 * - afterAgentResponse: append assistant message
 * - stop: rebuild a full readable transcript when transcript_path is available
 */

const fs = require("fs");
const path = require("path");

function readStdin() {
  return new Promise((resolve, reject) => {
    const chunks = [];
    process.stdin.setEncoding("utf8");
    process.stdin.on("data", (c) => chunks.push(c));
    process.stdin.on("end", () => resolve(chunks.join("")));
    process.stdin.on("error", reject);
  });
}

function projectRoot(payload) {
  const roots = payload.workspace_roots;
  if (Array.isArray(roots) && roots.length > 0 && roots[0]) {
    return roots[0];
  }
  // Project hooks run from the workspace root.
  return process.cwd();
}

function conversationsDir(root) {
  const dir = path.join(root, "conversations");
  fs.mkdirSync(dir, { recursive: true });
  return dir;
}

function stamp() {
  return new Date().toISOString().replace("T", " ").replace(/\.\d{3}Z$/, " UTC");
}

function safeId(id) {
  return String(id || "unknown").replace(/[^a-zA-Z0-9_-]/g, "_");
}

function logPath(root, conversationId) {
  return path.join(conversationsDir(root), `chat-${safeId(conversationId)}.txt`);
}

function latestPath(root) {
  return path.join(conversationsDir(root), "latest-chat.txt");
}

function appendBlock(filePath, title, body) {
  const text = String(body || "").trimEnd();
  if (!text) return;
  const block = `\n===== ${title} (${stamp()}) =====\n${text}\n`;
  fs.appendFileSync(filePath, block, "utf8");
}

function extractText(content) {
  if (typeof content === "string") return content;
  if (!Array.isArray(content)) return "";
  return content
    .map((part) => {
      if (typeof part === "string") return part;
      if (part && typeof part.text === "string") return part.text;
      if (part && part.type === "text" && typeof part.text === "string") return part.text;
      return "";
    })
    .filter(Boolean)
    .join("\n");
}

function stripUserQueryWrapper(text) {
  const match = String(text).match(/<user_query>\s*([\s\S]*?)\s*<\/user_query>/i);
  if (match) return match[1].trim();
  return String(text)
    .replace(/<timestamp>[\s\S]*?<\/timestamp>\s*/gi, "")
    .trim();
}

function transcriptToText(transcriptPath) {
  if (!transcriptPath || !fs.existsSync(transcriptPath)) return null;

  const lines = fs.readFileSync(transcriptPath, "utf8").split(/\r?\n/).filter(Boolean);
  const out = [];
  out.push(`# Conversation export`);
  out.push(`# Source: ${transcriptPath}`);
  out.push(`# Generated: ${stamp()}`);
  out.push("");

  for (const line of lines) {
    let row;
    try {
      row = JSON.parse(line);
    } catch {
      continue;
    }

    if (row.type === "turn_ended") {
      out.push(`----- turn ended (${row.status || "unknown"}) -----`);
      out.push("");
      continue;
    }

    const role = row.role || row.type;
    if (!role) continue;

    const message = row.message || {};
    const content = extractText(message.content || row.content || "");
    if (!content.trim()) continue;

    if (role === "user") {
      out.push("===== USER =====");
      out.push(stripUserQueryWrapper(content));
      out.push("");
    } else if (role === "assistant") {
      // Keep visible assistant text; skip pure tool-call payloads with no text.
      const cleaned = content
        .replace(/<\/?think>/gi, "")
        .trim();
      if (!cleaned) continue;
      out.push("===== ASSISTANT =====");
      out.push(cleaned);
      out.push("");
    }
  }

  return out.join("\n").trimEnd() + "\n";
}

function writeOutputs(root, conversationId, content) {
  const perChat = logPath(root, conversationId);
  const latest = latestPath(root);
  fs.writeFileSync(perChat, content, "utf8");
  fs.writeFileSync(latest, content, "utf8");
  return { perChat, latest };
}

function ensureHeader(filePath, conversationId) {
  if (fs.existsSync(filePath) && fs.statSync(filePath).size > 0) return;
  const header =
    `# ClickPlay conversation log\n` +
    `# conversation_id: ${conversationId || "unknown"}\n` +
    `# started: ${stamp()}\n`;
  fs.writeFileSync(filePath, header, "utf8");
}

async function main() {
  const raw = await readStdin();
  let payload = {};
  try {
    payload = raw.trim() ? JSON.parse(raw) : {};
  } catch (err) {
    process.stderr.write(`[save-conversation] invalid JSON: ${err.message}\n`);
    process.stdout.write("{}\n");
    return;
  }

  const event = payload.hook_event_name || "";
  const root = projectRoot(payload);
  const conversationId = payload.conversation_id || payload.session_id || "unknown";
  const file = logPath(root, conversationId);

  try {
    if (event === "beforeSubmitPrompt") {
      ensureHeader(file, conversationId);
      appendBlock(file, "USER", payload.prompt || "");
      fs.copyFileSync(file, latestPath(root));
      process.stdout.write(JSON.stringify({ continue: true }) + "\n");
      return;
    }

    if (event === "afterAgentResponse") {
      ensureHeader(file, conversationId);
      appendBlock(file, "ASSISTANT", payload.text || "");
      fs.copyFileSync(file, latestPath(root));
      process.stdout.write("{}\n");
      return;
    }

    if (event === "stop") {
      const fromTranscript = transcriptToText(payload.transcript_path);
      if (fromTranscript) {
        writeOutputs(root, conversationId, fromTranscript);
      } else if (fs.existsSync(file)) {
        fs.copyFileSync(file, latestPath(root));
      }
      process.stdout.write("{}\n");
      return;
    }

    // Unknown event: no-op
    process.stdout.write("{}\n");
  } catch (err) {
    process.stderr.write(`[save-conversation] ${err.stack || err.message}\n`);
    // Fail open so chat is never blocked by logging.
    if (event === "beforeSubmitPrompt") {
      process.stdout.write(JSON.stringify({ continue: true }) + "\n");
    } else {
      process.stdout.write("{}\n");
    }
  }
}

main();
