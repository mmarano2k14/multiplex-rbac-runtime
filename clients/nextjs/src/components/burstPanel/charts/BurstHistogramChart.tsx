"use client";

import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";

import { BurstHistogramBucket } from "../../../lib/console/burst/metric/BurstMetricsType";
import { JSX, useMemo } from "react";

type Props = {
  data: BurstHistogramBucket[];
};

const chartFontFamily =
  'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';

export function BurstHistogramChart({ data }: Props): JSX.Element {
  const hasData = data.some((x) => x.count > 0);

  const chartKey = useMemo(() => {
    return data.map((x) => `${x.label}:${x.count}`).join("|");
  }, [data]);

  if (!hasData) {
    return (
      <div className="burst-chart burst-chart--empty">
        No latency data yet.
      </div>
    );
  }

  return (
    <div className="burst-chart burst-chart--histogram">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart
          key={chartKey}
          data={data}
          margin={{ top: 8, right: 16, bottom: 8, left: 0 }}
          barCategoryGap="18%"
          style={{ fontFamily: chartFontFamily }}
        >
          <CartesianGrid
            strokeDasharray="3 3"
            vertical={false}
            stroke="#d1d5db"
          />

          <XAxis
            dataKey="label"
            tickLine={false}
            axisLine={false}
            interval={0}
            tick={{
              fontSize: 12,
              fill: "#6b7280",
              fontFamily: chartFontFamily,
              fontWeight: 500,
            }}
          />

          <YAxis
            tickLine={false}
            axisLine={false}
            allowDecimals={false}
            width={40}
            tick={{
              fontSize: 12,
              fill: "#6b7280",
              fontFamily: chartFontFamily,
              fontWeight: 500,
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
            formatter={(value) => [`${value} request(s)`, "Count"]}
            labelFormatter={(label) => `Latency: ${label}`}
          />

          <Bar
            dataKey="count"
            radius={[10, 10, 0, 0]}
            maxBarSize={72}
            isAnimationActive={false}
          >
            {data.map((entry, index) => (
              <Cell
                key={`${entry.label}-${entry.count}-${index}`}
                fill={
                  entry.count > 0
                    ? "#2962ff"   
                    : "#dbeafe" 
                }
              />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}