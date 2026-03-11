"use client";

import {
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { BurstMetricPoint } from "../../../lib/console/burst/metric/BurstMetricsType";
import { JSX } from "react";

type Props = {
  data: BurstMetricPoint[];
};

function formatDuration(ms?: number): string {
  if (typeof ms !== "number" || Number.isNaN(ms)) return "-";
  if (ms < 1000) return `${Math.round(ms)} ms`;
  return `${(ms / 1000).toFixed(2)} s`;
}

function formatElapsed(ms?: number): string {
  if (typeof ms !== "number" || Number.isNaN(ms)) return "-";
  return `${(ms / 1000).toFixed(0)}s`;
}

function formatRps(v?: number): string {
  if (typeof v !== "number" || Number.isNaN(v)) return "-";
  return v.toFixed(0);
}

export function BurstGraph({ data }: Props): JSX.Element {
  return (
    <div style={{ width: "100%", height: 260 }}>
      <ResponsiveContainer>
        <LineChart data={data} margin={{ top: 8, right: 16, left: 8, bottom: 8 }}>
          <CartesianGrid strokeDasharray="3 3" />

          <XAxis
            dataKey="elapsedMs"
            tickFormatter={(v) => formatElapsed(v)}
            minTickGap={32}
            tick={{ fontSize: 12 }}
          />

          <YAxis
            yAxisId="latency"
            tickFormatter={(v) => formatDuration(v)}
            width={70}
            tick={{ fontSize: 12 }}
            label={{ value: "Latency", angle: -90, position: "insideLeft" }}
          />

          <YAxis
            yAxisId="rps"
            orientation="right"
            tickFormatter={(v) => formatRps(v)}
            width={50}
            tick={{ fontSize: 12 }}
            label={{ value: "RPS", angle: 90, position: "insideRight" }}
          />

          <Tooltip
            labelFormatter={(value) => `t = ${formatElapsed(Number(value))}`}
            formatter={(value, name) => {
              if (name === "RPS") return [formatRps(Number(value)), name];
              return [formatDuration(Number(value)), name];
            }}
          />

          <Legend />

          <Line
            yAxisId="latency"
            type="monotoneX"
            dataKey="p50"
            name="p50"
            stroke="#00c853"
            strokeWidth={2}
            dot={false}
            isAnimationActive={false}
          />

          <Line
            yAxisId="latency"
            type="monotoneX"
            dataKey="p95"
            name="p95"
            stroke="#ff1744"
            strokeWidth={2}
            dot={false}
            isAnimationActive={false}
          />

          <Line
            yAxisId="rps"
            type="monotoneX"
            dataKey="rps"
            name="RPS"
            stroke="#2962ff"
            strokeWidth={1.5}
            dot={false}
            isAnimationActive={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}