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

const chartFontFamily =
  'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';

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
  const hasData = data.length > 0;

  if (!hasData) {
    return (
      <div className="burst-chart burst-chart--empty">
        No live metrics yet.
      </div>
    );
  }

  return (
    <div className="burst-chart burst-chart--line">
      <ResponsiveContainer width="100%" height="100%">
        <LineChart
          data={data}
          margin={{ top: 8, right: 16, left: 8, bottom: 8 }}
          style={{ fontFamily: chartFontFamily }}
        >
          <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#d1d5db" />

          <XAxis
            dataKey="elapsedMs"
            tickFormatter={(v) => formatElapsed(v)}
            minTickGap={32}
            tick={{
              fontSize: 12,
              fill: "#6b7280",
              fontFamily: chartFontFamily,
              fontWeight: 500,
            }}
            tickLine={false}
            axisLine={false}
          />

          <YAxis
            yAxisId="latency"
            tickFormatter={(v) => formatDuration(v)}
            width={70}
            tick={{
              fontSize: 12,
              fill: "#6b7280",
              fontFamily: chartFontFamily,
              fontWeight: 500,
            }}
            tickLine={false}
            axisLine={false}
            label={{
              value: "Latency",
              angle: -90,
              position: "insideLeft",
              style: {
                fontSize: 12,
                fill: "#6b7280",
                fontFamily: chartFontFamily,
                fontWeight: 500,
              },
            }}
          />

          <YAxis
            yAxisId="rps"
            orientation="right"
            tickFormatter={(v) => formatRps(v)}
            width={50}
            tick={{
              fontSize: 12,
              fill: "#6b7280",
              fontFamily: chartFontFamily,
              fontWeight: 500,
            }}
            tickLine={false}
            axisLine={false}
            label={{
              value: "RPS",
              angle: 90,
              position: "insideRight",
              style: {
                fontSize: 12,
                fill: "#6b7280",
                fontFamily: chartFontFamily,
                fontWeight: 500,
              },
            }}
          />

          <Tooltip
            cursor={{ opacity: 0.08 }}
            contentStyle={{
              borderRadius: 10,
              border: "1px solid #e5e7eb",
              boxShadow: "0 4px 14px rgba(0,0,0,0.08)",
              fontFamily: chartFontFamily,
              fontSize: 12,
            }}
            labelStyle={{
              fontFamily: chartFontFamily,
              fontSize: 12,
              fontWeight: 600,
              color: "#111827",
            }}
            itemStyle={{
              fontFamily: chartFontFamily,
              fontSize: 12,
              color: "#111827",
            }}
            labelFormatter={(value) => `t = ${formatElapsed(Number(value))}`}
            formatter={(value, name) => {
              if (name === "RPS") {
                return [formatRps(Number(value)), name];
              }

              return [formatDuration(Number(value)), name];
            }}
          />

          <Legend
            wrapperStyle={{
              fontSize: 12,
              fontFamily: chartFontFamily,
            }}
          />

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