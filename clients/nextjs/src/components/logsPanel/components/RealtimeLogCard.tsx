import React, { JSX } from "react";
import { LogBadge } from "./LogBadge";
import { RealtimeLogHelper } from "../helpers/RealtimeLogHelper";
import { RealtimeLogEntry } from "@/lib/infrastructure/logs/inMemoryLogType";


export type RealtimeLogCardProps = {
  log: RealtimeLogEntry;
};

export function RealtimeLogCard(props: RealtimeLogCardProps): JSX.Element {
  const { log: l } = props;

  const levelColor = RealtimeLogHelper.getLevelColor(l.level);
  const badges = RealtimeLogHelper.getBadges(l);

  return (
    <div
      style={{
        border: "1px solid #eee",
        borderRadius: 12,
        padding: 12,
        borderLeft: `4px solid ${levelColor}`,
        background: "#fff",
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", gap: 10 }}>
        <div
          style={{
            fontWeight: 700,
            display: "flex",
            gap: 8,
            alignItems: "center",
            flexWrap: "wrap",
          }}
        >
          {badges.map((badge) => (
            <LogBadge key={badge.label} badge={badge} />
          ))}

          <span>{l.eventName ?? "realtime-event"}</span>

          {l.level ? <code style={{ color: levelColor }}>{l.level}</code> : null}
        </div>

        <div style={{ fontSize: 12, opacity: 0.7 }}>{l.t}</div>
      </div>

      <div style={{ marginTop: 8, fontSize: 13 }}>
        {l.category && (
          <div>
            <b>Category:</b> <code>{l.category}</code>
          </div>
        )}

        {l.message && (
          <div style={{ marginTop: 6 }}>
            <b>Message:</b> <code>{l.message}</code>
          </div>
        )}

        {typeof l.payload !== "undefined" && (
          <details style={{ marginTop: 8 }}>
            <summary style={{ cursor: "pointer" }}>Payload</summary>
            <pre style={{ marginTop: 8, whiteSpace: "pre-wrap" }}>
              {JSON.stringify(l.payload, null, 2)}
            </pre>
          </details>
        )}
      </div>
    </div>
  );
}