import React, { JSX } from "react";
import { LogBadge as LogBadgeModel } from "../LogsPanelType";

export type LogBadgeProps = {
  badge: LogBadgeModel;
};

export function LogBadge(props: LogBadgeProps): JSX.Element {
  const { badge } = props;

  return (
    <span
      style={{
        fontSize: 11,
        padding: "2px 6px",
        borderRadius: 999,
        background: badge.background,
        color: badge.color,
        fontWeight: 700,
        whiteSpace: "nowrap",
      }}
    >
      {badge.label}
    </span>
  );
}