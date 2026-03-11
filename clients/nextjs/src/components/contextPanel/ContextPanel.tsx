"use client";

import React, { JSX } from "react";

export type ContextPanelProps = {
  disabled: boolean;

  demoUserId: string;
  contextKey: string;

  onDemoUserIdChange: (v: string) => void;
  onContextKeyChange: (v: string) => void;
  onGetContextClick: () => void;
  onClearClick: () => void;
};

export function ContextPanel(props: ContextPanelProps) : JSX.Element {
    const {
    demoUserId,
    contextKey,
    onDemoUserIdChange,
    onContextKeyChange,
  } = props;

  return (
    <div style={{ border: "1px solid #ddd", borderRadius: 12, padding: 12 }}>
        <div style={{ fontWeight: 700, marginBottom: 8 }}>Context</div>

        <div style={{ display: "grid", gap: 8 }}>
        <label
            style={{
            display: "grid",
            gridTemplateColumns: "140px 1fr",
            gap: 8,
            alignItems: "center",
            }}
        >
            <span style={{ fontSize: 13 }}>Demo UserId</span>
            <input
            value={demoUserId}
            onChange={(e) => onDemoUserIdChange(e.target.value)}
            disabled={true}
            style={{ padding: 8, borderRadius: 8, border: "1px solid #ccc" }}
            />
        </label>

        <label
            style={{
            display: "grid",
            gridTemplateColumns: "140px 1fr",
            gap: 8,
            alignItems: "center",
            }}
        >
            <span style={{ fontSize: 13 }}>X-Access-Context</span>
            <input
            value={contextKey}
            onChange={(e) => onContextKeyChange(e.target.value) }
            disabled={true}
            placeholder="auto from /demo/context, and auto-rotated"
            style={{ padding: 8, borderRadius: 8, border: "1px solid #ccc" }}
            />
        </label>

        <div style={{ fontSize: 12, opacity: 0.75 }}>
            Rotation is automatic: if API returns <code>x-access-context</code>, the client
            session updates the current key.
        </div>
        </div>
    </div>
  )
}