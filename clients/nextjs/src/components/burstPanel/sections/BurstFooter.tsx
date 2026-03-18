"use client";

import { JSX } from "react";

export type BurstFooterProps = {
  durations?: number[];
  metricPoints: number;
};

export function BurstFooter(props: BurstFooterProps): JSX.Element {
  const { durations, metricPoints } = props;

  const min =
    durations?.length
      ? Math.min(...durations).toFixed(2)
      : "-";

  const max =
    durations?.length
      ? Math.max(...durations).toFixed(2)
      : "-";

  return (
    <>
      <div style={{ fontSize: 12, opacity: 0.7 }}>
        Tip: use Single burst for brute contention,
        Maintained concurrency for sustained load,
        and Wave batches for fixed-size packet testing.
      </div>

      <div style={{ fontSize: 12, opacity: 0.8 }}>
        min latency: <b>{min}</b> ms {" | "}
        max latency: <b>{max}</b> ms {" | "}
        metric points: <b>{metricPoints}</b>
      </div>
    </>
  );
}