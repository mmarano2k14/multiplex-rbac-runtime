"use client";

import React, { JSX } from "react";

import { SpinnerStyles } from "@/components/ui/Spinner";
import { BurstPanel } from "@/components/burstPanel/BurstPanel";

import { useBurstController } from "@/lib/console/burst/useBurstController";
import { RequestPanel } from "@/components/requestPanel/RequestPanel";
import { ContextPanel } from "@/components/contextPanel/ContextPanel";
import { TargetPanel } from "@/components/targetPanel/TargetPanel";
import { LogsPanel } from "@/components/logsPanel/LogsPanel";
import { useConsoleContext } from "@/lib/console/contextProvider/useConsoleContext";
import { Button } from "@/components/ui/Button";


// ---------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------
export default function Page(): JSX.Element {
  // ------------------------------------------------------------
  // Console machine/controller
  // ------------------------------------------------------------

  const { state, actions, dispatch, api } = useConsoleContext();

  //const consoleCtl = useConsoleController(PRESETS);
  //const { state, dispatch, actions, api } = consoleCtl;

  // ------------------------------------------------------------
  // Burst machine/controller
  // ------------------------------------------------------------
  const burst = useBurstController(api);

  return (
    <div style={{ padding: 16, fontFamily: "ui-sans-serif, system-ui" }}>
      <SpinnerStyles />

      <h1 style={{ fontSize: 22, fontWeight: 700 }}>
        Multiplexed RBAC — HTTP Console (Next.js)
      </h1>

      {/* top status */}
      <div
        style={{
          marginTop: 8,
          display: "flex",
          gap: 12,
          alignItems: "center",
          flexWrap: "wrap",
        }}
      >
        <div style={{ fontSize: 12, opacity: 0.8 }}>
          State: <b>{state.status}</b>
        </div>

        <div style={{ fontSize: 12, opacity: 0.8 }}>
          Busy: <b>{state.busy ? "true" : "false"}</b>
        </div>

        {state.lastError && (
          <>
            <div style={{ fontSize: 12, color: "crimson" }}>
              <b>Error:</b> {state.lastError}
            </div>
            <Button disabled={state.busy} onClick={actions.resetError}>
              Dismiss
            </Button>
          </>
        )}
      </div>

      {/* ---------------------------------------------------------------- */}
      {/* Target + Context */}
      {/* ---------------------------------------------------------------- */}
      <div
        style={{
          display: "grid",
          gap: 12,
          gridTemplateColumns: "1fr 1fr",
          marginTop: 12,
        }}
      >
        {/* Target */}
        <TargetPanel 
          disabled={state.busy} 
          baseUrl={state.baseUrl} 
          onTargetChanges={(v) => dispatch({ type: "TargetChanged", baseUrl:v})}        
        />

        {/* Context */}
        <ContextPanel 
          disabled={state.busy} 
          demoUserId={state.demoUserId} 
          contextKey={state.contextKey} 
          onDemoUserIdChange={() => {}}
          onContextKeyChange={() => {}}
          onGetContextClick={actions.getContextKey} 
          onClearClick={() => dispatch({ type: "ContextChanged", contextKey: ""})}
        />
          
      </div>

      {/* ---------------------------------------------------------------- */}
      {/* RequestPanel (dumb) */}
      {/* ---------------------------------------------------------------- */}
      <RequestPanel
        disabled={state.busy}
        invoiceId={state.invoiceId}
        amount={state.amount}
        onInvoiceIdChange={(v) => dispatch({ type: "InvoiceChanged", invoiceId: v })}
        onAmountChange={(v) => dispatch({ type: "AmountChanged", amount: v })}
        onReadClick={actions.readInvoice}
        onRefundClick={actions.refundInvoice}
        onClearLogClick={actions.clearLogs}
      />

      {/* ---------------------------------------------------------------- */}
      {/* BurstPanel (dumb + burst machine) */}
      {/* ---------------------------------------------------------------- */}
      <BurstPanel
        disabled={state.busy}
        model={burst.model}
        onConfigure={burst.actions.configure}
        onStart={burst.actions.start}
        onStop={burst.actions.stop}
        onReset={burst.actions.reset}
      />

      {/* ---------------------------------------------------------------- */}
      {/* Logs */}
      {/* ---------------------------------------------------------------- */}
      <LogsPanel logs={state.logs} />
    </div>
  );
}