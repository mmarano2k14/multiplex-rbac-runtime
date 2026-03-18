import { ConsoleLogEntry } from "@/lib/logs/inMemoryLogType";
import React, { JSX, useEffect, useMemo, useState } from "react";


export type LogsTimelineProps = {
  logs: ConsoleLogEntry[];
  windowSeconds?: number;
};

export function LogsTimeline(props: LogsTimelineProps): JSX.Element {
  const { logs, windowSeconds = 10 } = props;

  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const timer = window.setInterval(() => {
      setNow(Date.now());
    }, 500);

    return () => {
      window.clearInterval(timer);
    };
  }, []);

  const points = useMemo(() => {
    const windowMs = windowSeconds * 1000;

    return logs
      .map((log) => {
        const ts = new Date(log.t).getTime();
        const age = now - ts;

        if (age > windowMs) {
          return null;
        }

        const position = 1 - age / windowMs;

        return {
          ...log,
          position,
        };
      })
      .filter(Boolean) as Array<ConsoleLogEntry & { position: number }>;
  }, [logs, now, windowSeconds]);

  function getColor(log: ConsoleLogEntry): string {
    if (log.kind === "http") {
      const status = log.status ?? 0;

      if (status >= 200 && status < 300) return "#0f9d58";
      if (status >= 400 && status < 500) return "#f29900";
      if (status >= 500) return "#d93025";

      return "#1a73e8";
    }

    const level = (log.level ?? "").toLowerCase();

    if (level === "error") return "#d93025";
    if (level === "warning") return "#f29900";

    return "#1a73e8";
  }

  return (
    <div
      style={{
        position: "relative",
        height: 40,
        border: "1px solid #eee",
        borderRadius: 10,
        background: "#fafafa",
        overflow: "hidden",
        marginBottom: 12,
      }}
    >
      {points.map((p) => {
        const left = p.position * 100;

        return (
          <div
            key={p.id}
            title={(p.kind === "http" ? p.name : p.eventName) ?? "event"}
            style={{
              position: "absolute",
              left: `${left}%`,
              top: "50%",
              transform: "translate(-50%, -50%)",
              width: 8,
              height: 8,
              borderRadius: "50%",
              background: getColor(p),
            }}
          />
        );
      })}
    </div>
  );
}