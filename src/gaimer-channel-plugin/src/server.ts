import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";
import { createServer, type Socket as NetSocket } from "net";
import { homedir } from "os";
import { join } from "path";
import { existsSync, unlinkSync, mkdirSync } from "fs";

const SOCKET_DIR = join(homedir(), "Library", "Application Support", "Gaimer");
const SOCKET_PATH = join(SOCKET_DIR, "gaimer-team.sock");

// ── MCP Server Setup ──────────────────────────────────────────────

const server = new Server(
  { name: "gaimer-channel", version: "1.0.0" },
  {
    capabilities: {
      experimental: { "claude/channel": {} },
      tools: {},
    },
    instructions:
      'Tasks from Gaimer arrive as <channel source="gaimer"> tags. Use submit_result and send_status tools to respond.',
  }
);

// ── Pipe State ────────────────────────────────────────────────────

let pipeSocket: NetSocket | null = null;
const pendingPermissions = new Map<string, { resolve: (approved: boolean) => void }>();

function writeToPipe(obj: Record<string, unknown>): void {
  if (!pipeSocket || pipeSocket.destroyed) return;
  pipeSocket.write(JSON.stringify(obj) + "\n");
}

// ── MCP Tools ─────────────────────────────────────────────────────

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [
    {
      name: "submit_result",
      description:
        "Submit the final result for a Gaimer task. Use this when you have completed the task.",
      inputSchema: {
        type: "object" as const,
        properties: {
          task_id: { type: "string", description: "The task ID from the channel message meta" },
          status: {
            type: "string",
            enum: ["complete", "error"],
            description: "Whether the task completed successfully or failed",
          },
          response: { type: "string", description: "The result text (voice-ready for voice format)" },
          actions_taken: {
            type: "array",
            items: { type: "string" },
            description: "List of tools/actions you used (optional)",
          },
          follow_up: { type: "string", description: "Suggested next step (optional)" },
          artifacts: {
            type: "array",
            items: {
              type: "object",
              properties: {
                type: { type: "string" },
                title: { type: "string" },
                content: { type: "string" },
              },
              required: ["type", "title", "content"],
            },
            description: "URLs, code, or data worth saving (optional)",
          },
        },
        required: ["task_id", "status", "response"],
      },
    },
    {
      name: "send_status",
      description:
        "Send a progress update for a long-running Gaimer task. Use for tasks taking >10 seconds.",
      inputSchema: {
        type: "object" as const,
        properties: {
          task_id: { type: "string", description: "The task ID from the channel message meta" },
          message: { type: "string", description: "Progress update message" },
        },
        required: ["task_id", "message"],
      },
    },
  ],
}));

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  if (name === "submit_result") {
    writeToPipe({
      type: "task_result",
      task_id: args?.task_id,
      status: args?.status,
      response: args?.response,
      actions_taken: args?.actions_taken ?? [],
      follow_up: args?.follow_up ?? null,
      artifacts: args?.artifacts ?? [],
    });
    return { content: [{ type: "text", text: "Result submitted to Gaimer." }] };
  }

  if (name === "send_status") {
    writeToPipe({
      type: "status_update",
      task_id: args?.task_id,
      message: args?.message,
    });
    return { content: [{ type: "text", text: "Status sent to Gaimer." }] };
  }

  return { content: [{ type: "text", text: `Unknown tool: ${name}` }], isError: true };
});

// ── Unix Socket Pipe Listener ─────────────────────────────────────

mkdirSync(SOCKET_DIR, { recursive: true });

if (existsSync(SOCKET_PATH)) {
  unlinkSync(SOCKET_PATH);
}

const pipeServer = createServer((socket) => {
  // Destroy stale connection before accepting new one (PB-M6)
  if (pipeSocket && !pipeSocket.destroyed) {
    pipeSocket.destroy();
  }
  pipeSocket = socket;
  let buffer = "";

  socket.on("data", (chunk) => {
    buffer += chunk.toString();
    const lines = buffer.split("\n");
    buffer = lines.pop() ?? "";

    for (const line of lines) {
      if (!line.trim()) continue;
      try {
        const msg = JSON.parse(line);
        handlePipeMessage(msg);
      } catch {
        // Malformed JSON — skip
      }
    }
  });

  socket.on("close", () => {
    if (pipeSocket === socket) pipeSocket = null;
  });

  socket.on("error", () => {
    if (pipeSocket === socket) pipeSocket = null;
  });
});

function handlePipeMessage(msg: Record<string, unknown>): void {
  const type = msg.type as string;

  if (type === "task_request") {
    const context = msg.context as Record<string, unknown> | undefined;
    server.notification({
      method: "notifications/message",
      params: {
        level: "info",
        data: {
          content: msg.task as string,
          source: "gaimer",
          meta: {
            task_id: msg.id,
            game: context?.game,
            agent: context?.agent,
            response_format: msg.response_format,
            l1: context?.l1_context,
            l2: context?.l2_context,
            recent_activity: context?.recent_activity,
          },
        },
      },
    });
    return;
  }

  if (type === "ping") {
    writeToPipe({ type: "pong" });
    return;
  }

  if (type === "permission_response") {
    const id = msg.id as string;
    const approved = msg.approved as boolean;
    const pending = pendingPermissions.get(id);
    if (pending) {
      pending.resolve(approved);
      pendingPermissions.delete(id);
    }
    return;
  }
}

/** Infrastructure for future permission integration — will be called by MCP tool hooks
 *  when Claude's session encounters a destructive action that needs user approval.
 *  Currently no caller; wired in Phase G as the outbound half of the permission round-trip. */
function requestPermission(taskId: string, action: string, risk: string, timeoutSeconds: number = 60): Promise<boolean> {
  const id = `perm_${crypto.randomUUID().slice(0, 12)}`;
  return new Promise<boolean>((resolve) => {
    pendingPermissions.set(id, { resolve });

    writeToPipe({
      type: "permission_request",
      id,
      task_id: taskId,
      action,
      risk,
      timeout_seconds: timeoutSeconds,
    });

    // Auto-deny on timeout
    setTimeout(() => {
      if (pendingPermissions.has(id)) {
        pendingPermissions.delete(id);
        resolve(false);
      }
    }, timeoutSeconds * 1000);
  });
}

// ── Startup ───────────────────────────────────────────────────────

pipeServer.listen(SOCKET_PATH, () => {
  // Socket ready — Gaimer can connect
});

const transport = new StdioServerTransport();
await server.connect(transport);

// Cleanup on exit
process.on("SIGTERM", () => {
  pipeServer.close();
  if (existsSync(SOCKET_PATH)) unlinkSync(SOCKET_PATH);
  process.exit(0);
});

process.on("SIGINT", () => {
  pipeServer.close();
  if (existsSync(SOCKET_PATH)) unlinkSync(SOCKET_PATH);
  process.exit(0);
});
