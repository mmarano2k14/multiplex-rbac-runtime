"use client";

import React, { JSX } from "react";
import { Button } from "@/components/ui/Button";

export type RequestPanelProps = {
  disabled: boolean;

  invoiceId: string;
  amount: number;

  onInvoiceIdChange: (v: string) => void;
  onAmountChange: (v: number) => void;

  onReadClick: () => void;
  onRefundClick: () => void;
  onClearLogClick: () => void;
};

export function RequestPanel(props: RequestPanelProps): JSX.Element {
  const {
    disabled,
    invoiceId,
    amount,
    onInvoiceIdChange,
    onAmountChange,
    onReadClick,
    onRefundClick,
    onClearLogClick,
  } = props;

  return (
    <div style={{ border: "1px solid #ddd", borderRadius: 12, padding: 12, marginTop: 12 }}>
      <div style={{ fontWeight: 700, marginBottom: 8 }}>Requests</div>

      <div style={{ display: "grid", gap: 8, gridTemplateColumns: "1fr 1fr 1fr" }}>
        <div>
          <div style={{ fontSize: 13, marginBottom: 6 }}>InvoiceId</div>
          <input
            value={invoiceId}
            onChange={(e) => onInvoiceIdChange(e.target.value)}
            disabled={disabled}
            style={{ padding: 8, borderRadius: 8, border: "1px solid #ccc", width: "100%" }}
          />
        </div>

        <div>
          <div style={{ fontSize: 13, marginBottom: 6 }}>Amount</div>
          <input
            type="number"
            value={amount}
            onChange={(e) => onAmountChange(Number(e.target.value))}
            disabled={disabled}
            style={{ padding: 8, borderRadius: 8, border: "1px solid #ccc", width: "100%" }}
          />
        </div>

        <div style={{ display: "flex", gap: 8, alignItems: "end" }}>
          <Button loading={disabled} disabled={disabled} onClick={onReadClick}>
            READ
          </Button>

          <Button loading={disabled} disabled={disabled} onClick={onRefundClick}>
            REFUND
          </Button>

          <Button disabled={disabled} onClick={onClearLogClick}>
            Clear Log
          </Button>
        </div>
      </div>
    </div>
  );
}