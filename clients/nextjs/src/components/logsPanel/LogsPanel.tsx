import { LogEntry } from "@/lib/logs/contracts";
import React, { JSX } from "react";


export type LogsPanelProps = {
  logs: LogEntry[];
};

export function LogsPanel(props: LogsPanelProps) : JSX.Element {

    const {
        logs,
    } = props

    return (
        <div style={{ marginTop: 12 }}>
        <div style={{ fontWeight: 700, marginBottom: 8 }}>Live log</div>

        <div style={{ display: "grid", gap: 10 }}>
          {logs.length === 0 && (
            <div style={{ fontSize: 13, opacity: 0.7 }}>
              No requests yet. Click <b>Get ContextKey</b>, then READ/REFUND.
            </div>
          )}

          {logs.map((l) => (
            <div key={l.id} style={{ border: "1px solid #eee", borderRadius: 12, padding: 12 }}>
              <div style={{ display: "flex", justifyContent: "space-between", gap: 10 }}>
                <div style={{ fontWeight: 700 }}>
                  {l.name} — <code>{l.method}</code> <code>{l.path}</code>
                </div>
                <div style={{ fontSize: 12, opacity: 0.7 }}>{l.t}</div>
              </div>

              <div style={{ marginTop: 8, fontSize: 13 }}>
                <div>
                  <b>Target:</b> <code>{l.baseUrl}</code>
                </div>

                {l.url && (
                  <div>
                    <b>Resolved URL:</b> <code>{l.url}</code>
                  </div>
                )}

                <div style={{ marginTop: 6 }}>
                  <b>Request headers:</b>{" "}
                  <code>
                    {Object.entries(l.requestHeaders)
                      .map(([k, v]) => `${k}=${v}`)
                      .join(" | ") || "(none)"}
                  </code>
                </div>

                {typeof l.requestBody !== "undefined" && (
                  <div style={{ marginTop: 6 }}>
                    <b>Request body:</b> <code>{JSON.stringify(l.requestBody)}</code>
                  </div>
                )}

                {l.error && (
                  <div style={{ marginTop: 8 }}>
                    <b style={{ color: "crimson" }}>Error:</b> <code>{l.error}</code>
                  </div>
                )}

                {typeof l.status !== "undefined" && (
                  <div style={{ marginTop: 8 }}>
                    <b>Response:</b>{" "}
                    <code>
                      {l.status} {l.statusText ?? ""}
                    </code>{" "}
                    <span style={{ marginLeft: 8 }}>{l.ok ? "✅ ok" : "❌ not ok"}</span>
                  </div>
                )}

                {l.rotation && (
                  <div style={{ marginTop: 6 }}>
                    <b>Rotation:</b> <code>{l.rotation.from}</code> → <code>{l.rotation.to}</code>
                  </div>
                )}

                {l.responseHeaders && Object.keys(l.responseHeaders).length > 0 && (
                  <div style={{ marginTop: 6 }}>
                    <b>Response headers:</b>{" "}
                    <code>
                      {Object.entries(l.responseHeaders)
                        .map(([k, v]) => `${k}=${v}`)
                        .join(" | ")}
                    </code>
                  </div>
                )}

                {typeof l.responseBody === "string" && l.responseBody.length > 0 && (
                  <details style={{ marginTop: 8 }}>
                    <summary style={{ cursor: "pointer" }}>Response body</summary>
                    <pre style={{ marginTop: 8, whiteSpace: "pre-wrap" }}>{l.responseBody}</pre>
                  </details>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
    )
}