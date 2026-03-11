import React from "react";

export class UiHelpers {

  public static Spinner({ size = 18 }: { size?: number }) {
    return (
      <span
        style={{
          display: "inline-block",
          width: size,
          height: size,
          borderRadius: "50%",
          border: "2px solid rgba(0,0,0,0.2)",
          borderTopColor: "rgba(0,0,0,0.7)",
          animation: "spin 0.8s linear infinite",
        }}
      />
    );
  }

  public static Stat({ label, value }: { label: string; value: React.ReactNode }) {
    return (
      <div style={{ padding: 10, border: "1px solid #eee", borderRadius: 10 }}>
        <div style={{ fontSize: 12, opacity: 0.7 }}>{label}</div>
        <div style={{ fontSize: 16, fontWeight: 600 }}>{value}</div>
      </div>
    );
  }

  public static ProgressBar({ value }: { value: number }) {
    const pct = Math.round(value * 100);
    return (
      <div style={{ height: 10, background: "#f3f3f3", borderRadius: 999 }}>
        <div
          style={{
            height: "100%",
            width: `${pct}%`,
            background: "#111",
            borderRadius: 999,
            transition: "width 120ms linear",
          }}
        />
      </div>
    );
  }

  public static ratio(done: number, total: number): number {
    if (total <= 0) return 0;
    const r = done / total;
    return Math.max(0, Math.min(1, r));
  }

  public static formatMs(v?: number): string {
    if (!v || v < 0) return "0 ms";
    if (v < 1000) return `${Math.round(v)} ms`;
    return `${(v / 1000).toFixed(2)} s`;
  }

  public static StyleOnce() {
    return (
      <style>
        {`
          @keyframes spin { to { transform: rotate(360deg); } }
        `}
      </style>
    );
  }
}