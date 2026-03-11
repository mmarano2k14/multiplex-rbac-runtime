"use client";

import React, { JSX } from "react";

export type SpinnerProps = {
  size?: number; // px
};

export function Spinner({ size = 16 }: SpinnerProps): JSX.Element {
  const border = Math.max(2, Math.floor(size / 8));

  return (
    <span
      aria-label="loading"
      style={{
        width: size,
        height: size,
        borderRadius: "999px",
        border: `${border}px solid rgba(0,0,0,0.15)`,
        borderTop: `${border}px solid rgba(0,0,0,0.65)`,
        display: "inline-block",
        animation: "spin 0.9s linear infinite",
      }}
    />
  );
}

// Global style injection (small & simple)
export function SpinnerStyles(): JSX.Element {
  return (
    <style>{`
      @keyframes spin {
        from { transform: rotate(0deg); }
        to   { transform: rotate(360deg); }
      }
    `}</style>
  );
}