"use client";

import React, { JSX } from "react";
import { Spinner } from "./Spinner";

export type ButtonProps = {
  children: React.ReactNode;
  onClick?: () => void;
  disabled?: boolean;
  loading?: boolean;
  variant?: "primary" | "neutral";
  title?: string;
};

export function Button({
  children,
  onClick,
  disabled,
  loading,
  variant = "neutral",
  title,
}: ButtonProps): JSX.Element {
  const isDisabled = Boolean(disabled || loading);

  const base: React.CSSProperties = {
    padding: "8px 10px",
    borderRadius: 10,
    border: "1px solid #ccc",
    cursor: isDisabled ? "not-allowed" : "pointer",
    opacity: isDisabled ? 0.6 : 1,
    display: "inline-flex",
    gap: 8,
    alignItems: "center",
    justifyContent: "center",
    userSelect: "none",
  };

  const style: React.CSSProperties =
    variant === "primary"
      ? { ...base, background: "#111", color: "#fff", border: "1px solid #111" }
      : { ...base, background: "#fff", color: "#111" };

  return (
    <button type="button" title={title} onClick={onClick} disabled={isDisabled} style={style}>
      {loading ? <Spinner size={16} /> : null}
      {children}
    </button>
  );
}