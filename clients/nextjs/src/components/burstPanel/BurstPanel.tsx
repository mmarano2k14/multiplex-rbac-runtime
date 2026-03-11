/* eslint-disable react-hooks/purity */
"use client";

import { JSX, useMemo } from "react";
import { UiHelpers } from "./uiHelpers";

import type { BurstModel, BurstConfig, BurstPlanKey } from "@/lib/console/burst/BurstMachineType";
import { BurstHistogram } from "@/lib/console/burst/metric/BurstHistogram";
import { useBurstMetricsSampler } from "@/lib/console/burst/metric/useBurstMetricsSampler";
import { BurstGraph } from "./charts/BurstGraph";
import { BurstHistogramChart } from "./charts/BurstHistogramChart";
import { Button } from "../ui/Button";

export type BurstPanelProps = {
  disabled: boolean;

  model: BurstModel;

  onConfigure: (cfg: BurstConfig) => void;
  onStart: () => void;
  onStop: () => void;
  onReset: () => void;
};

export function BurstPanel(props: BurstPanelProps): JSX.Element {
  const { disabled, model, onConfigure, onStart, onStop, onReset } = props;

  const report = model.report;
  const state = model.state;

  const isRunning = state === "Running" || state === "Stopping";

  const cfg = report?.config ?? {
    planKey: "read" as BurstPlanKey,
    total: 0,
    concurrency: 0,
    delayMs: 0,
  };

  const progress = report?.progress;
  const counters = report?.counters;

  const completed = progress?.completed ?? 0;
  const started = progress?.started ?? 0;
  const inFlight = progress?.inFlight ?? 0;

  const ratio = UiHelpers.ratio(completed, cfg.total);

  const elapsed = (() => {
    if (!report?.timing.startedAt) return undefined;
    const end = report.timing.finishedAt ?? Date.now();
    return Math.max(0, end - report.timing.startedAt);
  })();

  const metrics = useBurstMetricsSampler(model);

  const histogram = useMemo(() => {
    return BurstHistogram.build(report?.stats.durationsMs ?? []);
  }, [report?.stats.durationsMs]);

  function update(partial: Partial<BurstConfig>) {
    onConfigure({
      planKey: cfg.planKey,
      total: cfg.total,
      concurrency: cfg.concurrency,
      delayMs: cfg.delayMs,
      ...partial,
    });
  }

  const showCharts =
    isRunning || (report?.stats.durationsMs?.length ?? 0) > 0;

  return (
    <div
      style={{
        display: "grid",
        gap: 14,
        border: "1px solid #ddd",
        borderRadius: 12,
        padding: 12,
        marginTop: 12,
      }}
    >
      {/* Header */}
      <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
        <div style={{ fontSize: 18, fontWeight: 700 }}>Burst / In-Flight Demo</div>
        <div style={{ marginLeft: "auto", fontSize: 12, opacity: 0.75 }}>
          State: <b>{state}</b>
        </div>
      </div>

      {/* Controls */}
      <div style={{ display: "grid", gap: 10, gridTemplateColumns: "1fr 1fr 1fr 1fr" }}>
        <div style={{ display: "grid", gap: 6 }}>
          <label style={{ fontSize: 12, opacity: 0.75 }}>Plan</label>
          <select
            value={cfg.planKey}
            disabled={disabled || isRunning}
            onChange={(e) => update({ planKey: e.target.value as BurstPlanKey })}
            style={{ padding: 10, borderRadius: 10, border: "1px solid #ddd" }}
          >
            <option value="read">Invoice.Read (GET)</option>
            <option value="refund">Invoice.Refund (POST)</option>
          </select>
        </div>

        <div style={{ display: "grid", gap: 6 }}>
          <label style={{ fontSize: 12, opacity: 0.75 }}>Total requests</label>
          <input
            type="number"
            value={cfg.total}
            disabled={disabled || isRunning}
            onChange={(e) => update({ total: Number(e.target.value) })}
            style={{ padding: 10, borderRadius: 10, border: "1px solid #ddd" }}
          />
        </div>

        <div style={{ display: "grid", gap: 6 }}>
          <label style={{ fontSize: 12, opacity: 0.75 }}>Concurrency</label>
          <input
            type="number"
            value={cfg.concurrency}
            disabled={disabled || isRunning}
            onChange={(e) => update({ concurrency: Number(e.target.value) })}
            style={{ padding: 10, borderRadius: 10, border: "1px solid #ddd" }}
          />
        </div>

        <div style={{ display: "grid", gap: 6 }}>
          <label style={{ fontSize: 12, opacity: 0.75 }}>Delay per request (ms)</label>
          <input
            type="number"
            value={cfg.delayMs}
            disabled={disabled || isRunning}
            onChange={(e) => update({ delayMs: Number(e.target.value) })}
            style={{ padding: 10, borderRadius: 10, border: "1px solid #ddd" }}
          />
        </div>
      </div>

      {/* Buttons */}
      <div style={{ display: "flex", gap: 10 }}>
        <Button disabled={disabled || isRunning} onClick={onStart}>
          Start Burst
        </Button>

        <Button disabled={disabled || !isRunning} onClick={onStop}>
          Stop
        </Button>

        <Button disabled={disabled || isRunning} onClick={onReset}>
          Reset
        </Button>

        <div style={{ marginLeft: "auto", fontSize: 12, opacity: 0.75, alignSelf: "center" }}>
          elapsed: <b>{UiHelpers.formatMs(elapsed)}</b>
        </div>
      </div>

      {/* Progress */}
      <div style={{ display: "grid", gap: 8 }}>
        <UiHelpers.ProgressBar value={ratio} />
        <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12, opacity: 0.7 }}>
          <span>
            started: <b>{started}</b> — completed: <b>{completed}</b> — inFlight: <b>{inFlight}</b>
          </span>
          <span>{Math.round(ratio * 100)}%</span>
        </div>
      </div>

      {/* Stats */}
      <div style={{ display: "grid", gap: 10, gridTemplateColumns: "repeat(6, 1fr)" }}>
        <UiHelpers.Stat label="OK" value={counters?.ok ?? 0} />
        <UiHelpers.Stat label="401" value={counters?.unauthorized ?? 0} />
        <UiHelpers.Stat label="403" value={counters?.forbidden ?? 0} />
        <UiHelpers.Stat label="Other" value={counters?.other ?? 0} />
        <UiHelpers.Stat label="Errors" value={counters?.errors ?? 0} />
        <UiHelpers.Stat
          label="p50 / p95"
          value={`${UiHelpers.formatMs(report?.stats.p50ms)} / ${UiHelpers.formatMs(
            report?.stats.p95ms
          )}`}
        />
      </div>

      {/* Live Charts */}
      {showCharts && (
        <div style={{ display: "grid", gap: 16 }}>
          <div>
            <div style={{ fontWeight: 700, marginBottom: 8 }}>Live Metrics</div>
            <BurstGraph data={metrics} />
          </div>

          <div>
            <div style={{ fontWeight: 700, marginBottom: 8 }}>Latency Histogram</div>
            <BurstHistogramChart data={histogram} />
          </div>
        </div>
      )}

      {/* Error */}
      {report?.error && (
        <div
          style={{
            padding: 12,
            borderRadius: 10,
            border: "1px solid #f2c2c2",
            background: "#fff5f5",
            color: "#7a1f1f",
            fontSize: 13,
          }}
        >
          <b>Error:</b> {report.error}
        </div>
      )}

      <div style={{ fontSize: 12, opacity: 0.7 }}>
        Tip: Total=500, Concurrency=50, Delay=0. In-flight should spike near concurrency then drop to 0.
      </div>


      <div style={{ fontSize: 12, opacity: 0.8 }}>
        min latency:{" "}
        <b>
          {report?.stats.durationsMs?.length
            ? Math.min(...report.stats.durationsMs).toFixed(2)
            : "-"}
        </b>{" "}
        ms
        {" | "}
        max latency:{" "}
        <b>
          {report?.stats.durationsMs?.length
            ? Math.max(...report.stats.durationsMs).toFixed(2)
            : "-"}
        </b>{" "}
        ms
        {" | "}
        metric points: <b>{metrics.length}</b>
      </div>

    </div>
  );
}