import React, { JSX, useState } from "react";
import { LogBadge } from "./LogBadge";
import { HttpLogHelper } from "../helpers/HttpLogHelper";
import { HttpLogEntry } from "@/lib/infrastructure/logs/inMemoryLogType";


export type HttpLogCardProps = {
  log: HttpLogEntry;
};

export function HttpLogCard(props: HttpLogCardProps): JSX.Element {
  const { log: l } = props;
  const [open, setOpen] = useState(false);

  const badges = HttpLogHelper.getBadges(l);
  const statusColor = HttpLogHelper.getStatusColor(l.status);

  return (
    <div
      style={{
        border: "1px solid #eee",
        borderRadius: 10,
        overflow: "hidden",
        background: "#fff",
      }}
    >
      <div
        onClick={() => setOpen((x) => !x)}
        style={{
          cursor: "pointer",
          padding: 10,
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          background: "#fafafa",
          borderLeft: `4px solid ${statusColor}`,
        }}
      >
        <div
          style={{
            display: "flex",
            gap: 8,
            alignItems: "center",
            minWidth: 0,
            flexWrap: "wrap",
          }}
        >
          <span style={{ fontSize: 12 }}>{open ? "▼" : "▶"}</span>

          {badges.map((badge) => (
            <LogBadge key={badge.label} badge={badge} />
          ))}

          <b>{l.method}</b>
          <code style={{ overflow: "hidden", textOverflow: "ellipsis" }}>{l.path}</code>
        </div>

        <div style={{ display: "flex", gap: 10, alignItems: "center", fontSize: 12 }}>
          {typeof l.status !== "undefined" && (
            <span
              style={{
                color: statusColor,
                fontWeight: 700,
                whiteSpace: "nowrap",
              }}
            >
              {l.status} {l.statusText ?? ""}
            </span>
          )}

          <span style={{ opacity: 0.7, whiteSpace: "nowrap" }}>{l.t}</span>
        </div>
      </div>

      {open && (
        <div style={{ padding: 12, fontSize: 13 }}>
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
              {Object.entries(l.requestHeaders ?? {})
                .map(([k, v]) => `${k}=${v}`)
                .join(" | ") || "(none)"}
            </code>
          </div>

          {typeof l.requestBody !== "undefined" && (
            <div style={{ marginTop: 6 }}>
              <b>Request body:</b>
              <pre style={{ marginTop: 6, whiteSpace: "pre-wrap" }}>
                {JSON.stringify(l.requestBody, null, 2)}
              </pre>
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
      )}
    </div>
  );
}