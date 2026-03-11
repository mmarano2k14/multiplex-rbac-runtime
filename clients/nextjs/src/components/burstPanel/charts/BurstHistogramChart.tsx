"use client";

import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";

import { BurstHistogramBucket } from "../../../lib/console/burst/metric/BurstMetricsType";
import { JSX } from "react";


type Props = {
  data: BurstHistogramBucket[];
};

export function BurstHistogramChart({ data }: Props) : JSX.Element {
  return (
    <div style={{ width: "100%", height: 220 }}>
      <ResponsiveContainer>
        <BarChart data={data}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="label" />
          <YAxis />
          <Tooltip />
          <Bar dataKey="count" fill="#111" />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}