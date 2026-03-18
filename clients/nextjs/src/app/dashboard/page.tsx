"use client";

import { JSX, useState } from "react";

import { BurstPanel } from "@/components/burstPanel/BurstPanel";
import { useBurstController } from "@/lib/console/burst/useBurstController";

import { RequestPanel } from "@/components/requestPanel/RequestPanel";
import { ContextPanel } from "@/components/contextPanel/ContextPanel";
import { TargetPanel } from "@/components/targetPanel/TargetPanel";
import { LogsPanel } from "@/components/logsPanel/LogsPanel";

import { useConsoleContext } from "@/lib/console/contextProvider/useConsoleContext";

import { BurstScenarioDefinition } from "@/lib/console/burst/scenarios/BurstScenarioPresetType";
import { InFlightMaxValue } from "@/lib/console/ConsoleType";

import { ScenarioPresetsPanel } from "@/components/burstPanel/scenarios/ScenarioPresetsPanel";
import { BurstPanelForm } from "@/components/burstPanel/BurstPanelForm";
import { BottomDrawer } from "@/components/ui/BottomDrawer";
import { ConsoleStatusBar } from "@/components/ui/status/ConsoleStatusBar";
import { BurstPanelHelpers } from "@/components/burstPanel/helpers/BurstPanelHelpers";
import { BurstActions } from "@/components/burstPanel/sections/BurstActions";

type SidebarTabKey = "scenarios" | "request" | "burst";

export default function Page(): JSX.Element {
  const { state, actions, dispatch, api } = useConsoleContext();
  const burst = useBurstController({ api, dispatch });

  const [isLogsCollapsed, setIsLogsCollapsed] = useState(false);
  const [logsHeight, setLogsHeight] = useState(320);

  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false);
  const [activeSidebarTab, setActiveSidebarTab] = useState<SidebarTabKey>("scenarios");

  const isRunning = BurstPanelHelpers.isRunning(burst.model);

  async function handleLaunchScenario(scenario: BurstScenarioDefinition) {
  
    try {
      if(scenario.maxInFlight){
        dispatch({
          type: "maxInFlightChanged",
          maxInFlightValue: scenario.maxInFlight as InFlightMaxValue,
        });
      }

      if(scenario.rotationOverlapMs){
        dispatch({
          type: "rotationOverlapMsChange",
          rotationOverlapMs: scenario.rotationOverlapMs,
        });
      }

      await burst.actions.start(scenario.burstConfig);
    } finally {
      
    }
  }


  function handleSidebarTabClick(tab: SidebarTabKey): void {
    setActiveSidebarTab(tab);
    setIsSidebarCollapsed(false);
  }

  return (
    <div className="console-shell">
      <header className="console-header">
        <div className="header-left">
          <TargetPanel
            disabled={state.busy}
            baseUrl={state.baseUrl}
            onTargetChanges={(v) =>
              dispatch({ type: "TargetChanged", baseUrl: v })
            }
          />
          <ConsoleStatusBar
            status={burst.model.state}
            busy={isRunning}
            lastError={state.lastError}
            username={state.demoUserId}
            contextKey={api.contextKey}
            onDismissError={actions.resetError}
          />  

        </div>

        <div className="header-right">
          <ContextPanel
            disabled={isRunning}
            demoUserId={state.demoUserId}
            contextKey={api.contextKey}
            maxInFlight={state.maxInFlight}
            rotationOverlapMs={state.rotationOverlapMs}
            onDemoUserIdChange={() => {}}
            onContextKeyChange={() => {}}
            onGetContextClick={actions.getContextKey}
            onMaxInFlightChange={(v) =>
              dispatch({ type: "maxInFlightChanged", maxInFlightValue: v })
            }
            onRotationOverlapMsChange={(v) =>
              dispatch({ type: "rotationOverlapMsChange", rotationOverlapMs: v })
            }
            onClearClick={() =>
              dispatch({ type: "ContextChanged", contextKey: "" })
            }
          />

          <BurstActions 
            disabled={isRunning}
            isRunning={isRunning}
            onStart={burst.actions.start}
            onStop={burst.actions.stop}
            onReset={burst.actions.reset}
          /> 

        </div>
      </header>

      <div
        className="console-content"
        style={{
          paddingBottom: isLogsCollapsed ? "56px" : `${logsHeight}px`,
        }}
      >
        <div
          className={
            isSidebarCollapsed
              ? "console-body console-body--sidebar-collapsed"
              : "console-body"
          }
        >
          <aside
            className={
              isSidebarCollapsed
                ? "console-sidebar console-sidebar--collapsed"
                : "console-sidebar"
            }
          >
            <div className="console-sidebar__header">
              {!isSidebarCollapsed && (
                <div className="console-sidebar__title">Controls</div>
              )}

              <button
                type="button"
                className="console-sidebar__toggle"
                onClick={() => setIsSidebarCollapsed((v) => !v)}
                aria-expanded={!isSidebarCollapsed}
              >
                {isSidebarCollapsed ? "▶" : "◀"}
              </button>
            </div>

            {isSidebarCollapsed ? (
              <div className="console-sidebar__collapsed-tabs">
                <button
                  type="button"
                  className={
                    activeSidebarTab === "scenarios"
                      ? "console-sidebar__mini-tab active"
                      : "console-sidebar__mini-tab"
                  }
                  onClick={() => handleSidebarTabClick("scenarios")}
                  title="Scenario Presets"
                >
                  S
                </button>

                <button
                  type="button"
                  className={
                    activeSidebarTab === "request"
                      ? "console-sidebar__mini-tab active"
                      : "console-sidebar__mini-tab"
                  }
                  onClick={() => handleSidebarTabClick("request")}
                  title="Request"
                >
                  R
                </button>

                <button
                  type="button"
                  className={
                    activeSidebarTab === "burst"
                      ? "console-sidebar__mini-tab active"
                      : "console-sidebar__mini-tab"
                  }
                  onClick={() => handleSidebarTabClick("burst")}
                  title="Burst"
                >
                  B
                </button>
              </div>
            ) : (
              <div className="console-sidebar__content">
                <ScenarioPresetsPanel
                  planKey="read"
                  onLaunch={handleLaunchScenario}
                />

                <RequestPanel
                  disabled={isRunning}
                  invoiceId={state.invoiceId}
                  amount={state.amount}
                  onInvoiceIdChange={(v) =>
                    dispatch({ type: "InvoiceChanged", invoiceId: v })
                  }
                  onAmountChange={(v) =>
                    dispatch({ type: "AmountChanged", amount: v })
                  }
                  onReadClick={handleLaunchScenario}
                  onRefundClick={handleLaunchScenario}
                  onClearLogClick={actions.clearLogs}
                />

                <BurstPanelForm
                  disabled={state.busy}
                  model={burst.model}
                  onConfigure={burst.actions.configure}
                  onStart={burst.actions.start}
                  onStop={burst.actions.stop}
                  onReset={burst.actions.reset}
                />
              </div>
            )}
          </aside>

          <main className="console-main">
            <BurstPanel
              disabled={state.busy}
              model={burst.model}
              onConfigure={burst.actions.configure}
              onStart={burst.actions.start}
              onStop={burst.actions.stop}
              onReset={burst.actions.reset}
            />
          </main>
        </div>
      </div>

      <BottomDrawer
        title="Live log"
        isCollapsed={isLogsCollapsed}
        height={logsHeight}
        minHeight={140}
        maxHeight={620}
        onCollapsedChange={setIsLogsCollapsed}
        onHeightChange={setLogsHeight}
      >
        <LogsPanel 
          logs={state.logs}  
          onClearClick={actions.clearLogs}  
        />
      </BottomDrawer>
    </div>
  );
}