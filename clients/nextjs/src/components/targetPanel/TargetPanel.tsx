"use client";

import React, { JSX } from "react";
import { TargetPreset } from "@/lib/http/HttpClientType";

// ---------------------------------------------------------------------
// Presets
// ---------------------------------------------------------------------
const PRESETS: TargetPreset[] = [
  { label: ".NET (Kestrel)", baseUrl: "http://localhost:5000" },
  { label: "Java (Spring)", baseUrl: "http://localhost:8080" },
  { label: "Node", baseUrl: "http://localhost:3001" },
];


export type TargetPanelProps = {
  disabled: boolean;
  baseUrl:string;

  onTargetChanges: (v: string) => void;
};

export function TargetPanel(props: TargetPanelProps) : JSX.Element{
    const {
        disabled,
        baseUrl,
        onTargetChanges
    } = props

    return (
        <div style={{ border: "1px solid #ddd", borderRadius: 12, padding: 12 }}>
            <div style={{ fontWeight: 700, marginBottom: 8 }}>Target</div>

            <select
            value={baseUrl}
            onChange={(e) => onTargetChanges(e.target.value)}
            disabled={true}
            style={{ padding: 8, borderRadius: 8, border: "1px solid #ccc", width: "100%" }}
            >
            {PRESETS.map((p) => (
                <option key={p.baseUrl} value={p.baseUrl}>
                {p.label} — {p.baseUrl}
                </option>
            ))}
            </select>

            <div style={{ marginTop: 8 }}>
            <input
                value={baseUrl}
                onChange={(e) => onTargetChanges(e.target.value)}
                disabled={true}
                style={{ padding: 8, borderRadius: 8, border: "1px solid #ccc", width: "100%" }}
                placeholder="http://localhost:5000"
            />
            </div>

            <div style={{ marginTop: 12, fontSize: 12, opacity: 0.75 }}>
            UI → <code>/api/proxy</code> → backend target.
            </div>
        </div>
    )
}