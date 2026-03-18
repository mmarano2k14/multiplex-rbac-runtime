"use client";

import React, { JSX, useState } from "react";
import { HttpLogEntry } from "@/lib/logs/inMemoryLogType";
import { LogBadge } from "./LogBadge";
import { HttpLogHelper } from "../helpers/HttpLogHelper";

export type ContextRotationLogCardProps = {
  log: HttpLogEntry & {
    rotation: {
      from: string;
      to: string;
    };
  };
};

function shortKey(v?: string): string {
  if (!v) return "-";
  if (v.length <= 20) return v;
  return `${v.slice(0, 12)}...${v.slice(-6)}`;
}

export function ContextRotationLogCard(
  props: ContextRotationLogCardProps
): JSX.Element {
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
          gap: 12,
        }}
      >
        <div
          style={{
            display: "grid",
            gap: 6,
            minWidth: 0,
            flex: 1,
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

            {l.method && <b>{l.method}</b>}

            {l.path && (
              <code style={{ overflow: "hidden", textOverflow: "ellipsis" }}>
                {l.path}
              </code>
            )}
          </div>

          <div
            style={{
              display: "flex",
              gap: 8,
              alignItems: "center",
              flexWrap: "wrap",
              fontSize: 12,
            }}
          >
            <span style={{ opacity: 0.7 }}>Rotation</span>
            <code>{shortKey(l.rotation.from)}</code>
            <span style={{ opacity: 0.7 }}>→</span>
            <code>{shortKey(l.rotation.to)}</code>
          </div>
        </div>

        <div
          style={{
            display: "flex",
            gap: 10,
            alignItems: "center",
            fontSize: 12,
            whiteSpace: "nowrap",
          }}
        >
          {typeof l.status !== "undefined" && (
            <span
              style={{
                color: statusColor,
                fontWeight: 700,
              }}
            >
              {l.status} {l.statusText ?? ""}
            </span>
          )}

          <span style={{ opacity: 0.7 }}>{l.t}</span>
        </div>
      </div>

      {open && (
        <div style={{ padding: 12, fontSize: 13, display: "grid", gap: 10 }}>
          <div
            style={{
              border: "1px solid #eee",
              borderRadius: 10,
              padding: 10,
              background: "#fafafa",
              display: "grid",
              gap: 6,
            }}
          >
            <div style={{ fontWeight: 700 }}>Rotation Summary</div>

            <div>
              <b>From:</b> <code>{l.rotation.from}</code>
            </div>

            <div>
              <b>To:</b> <code>{l.rotation.to}</code>
            </div>

            <div>
              <b>Request:</b>{" "}
              <code>
                {l.method} {l.path}
              </code>
            </div>

            {typeof l.status !== "undefined" && (
              <div>
                <b>Status:</b>{" "}
                <code>
                  {l.status} {l.statusText ?? ""}
                </code>{" "}
                <span style={{ marginLeft: 8 }}>{l.ok ? "✅ ok" : "❌ not ok"}</span>
              </div>
            )}
          </div>

          <div>
            <b>Target:</b> <code>{l.baseUrl}</code>
          </div>

          {l.url && (
            <div>
              <b>Resolved URL:</b> <code>{l.url}</code>
            </div>
          )}

          <div>
            <b>Request headers:</b>{" "}
            <code>
              {Object.entries(l.requestHeaders ?? {})
                .map(([k, v]) => `${k}=${v}`)
                .join(" | ") || "(none)"}
            </code>
          </div>

          {typeof l.requestBody !== "undefined" && (
            <div>
              <b>Request body:</b>
              <pre style={{ marginTop: 6, whiteSpace: "pre-wrap" }}>
                {JSON.stringify(l.requestBody, null, 2)}
              </pre>
            </div>
          )}

          {l.responseHeaders && Object.keys(l.responseHeaders).length > 0 && (
            <div>
              <b>Response headers:</b>{" "}
              <code>
                {Object.entries(l.responseHeaders)
                  .map(([k, v]) => `${k}=${v}`)
                  .join(" | ")}
              </code>
            </div>
          )}

          {l.error && (
            <div>
              <b style={{ color: "crimson" }}>Error:</b> <code>{l.error}</code>
            </div>
          )}

          {typeof l.responseBody === "string" && l.responseBody.length > 0 && (
            <details>
              <summary style={{ cursor: "pointer" }}>Response body</summary>
              <pre style={{ marginTop: 8, whiteSpace: "pre-wrap" }}>{l.responseBody}</pre>
            </details>
          )}
        </div>
      )}
    </div>
  );
}