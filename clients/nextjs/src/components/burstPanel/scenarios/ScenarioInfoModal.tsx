"use client";

import { BurstScenarioDefinition } from "@/lib/console/burst/scenarios/BurstScenarioPresetType";
import { JSX } from "react";

export type ScenarioInfoModalProps = {
  scenario: BurstScenarioDefinition | null;
  isOpen: boolean;
  onClose: () => void;
  onLaunch: (scenario: BurstScenarioDefinition) => void;
};

function Section(props: {
  title: string;
  children: React.ReactNode;
}): JSX.Element {
  const { title, children } = props;

  return (
    <section
      style={{
        display: "grid",
        gap: 10,
        padding: 14,
        border: "1px solid #eee",
        borderRadius: 12,
        background: "#fafafa",
      }}
    >
      <div
        style={{
          fontWeight: 700,
          fontSize: 15,
        }}
      >
        {title}
      </div>

      <div style={{ fontSize: 14, lineHeight: 1.55 }}>{children}</div>
    </section>
  );
}

export function ScenarioInfoModal(
  props: ScenarioInfoModalProps
): JSX.Element | null {
  const { scenario, isOpen, onClose, onLaunch } = props;

  if (!isOpen || !scenario) {
    return null;
  }

  return (
    <div
      onClick={onClose}
      style={{
        position: "fixed",
        inset: 0,
        background: "rgba(0, 0, 0, 0.35)",
        display: "grid",
        placeItems: "center",
        zIndex: 2000,
        padding: 20,
      }}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        style={{
          width: "min(920px, 100%)",
          maxHeight: "90vh",
          overflowY: "auto",
          background: "#fff",
          borderRadius: 16,
          border: "1px solid #ddd",
          boxShadow: "0 20px 60px rgba(0,0,0,0.15)",
          display: "grid",
          gap: 18,
          padding: 20,
        }}
      >
        {/* Header */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            gap: 16,
            alignItems: "start",
            flexWrap: "wrap",
          }}
        >
          <div style={{ display: "grid", gap: 6 }}>
            <div style={{ fontSize: 24, fontWeight: 800 }}>
              {scenario.title}
            </div>

            <div
              style={{
                fontSize: 13,
                opacity: 0.7,
              }}
            >
              Scenario preset
            </div>
          </div>

          <div style={{ display: "flex", gap: 10 }}>
            <button
              type="button"
              onClick={() => onLaunch(scenario)}
              style={{
                padding: "10px 14px",
                borderRadius: 10,
                border: "1px solid #111",
                background: "#111",
                color: "#fff",
                cursor: "pointer",
                fontWeight: 700,
              }}
            >
              Launch
            </button>

            <button
              type="button"
              onClick={onClose}
              style={{
                padding: "10px 14px",
                borderRadius: 10,
                border: "1px solid #ddd",
                background: "#fff",
                cursor: "pointer",
                fontWeight: 600,
              }}
            >
              Close
            </button>
          </div>
        </div>

        {/* Idea */}
        <Section title="Idea">
          <div>{scenario.idea}</div>
        </Section>

        {/* Parameters */}
        <Section title="Recommended parameters">
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "220px 1fr",
              gap: 10,
              alignItems: "start",
            }}
          >
            {scenario.recommendedParameters.map((parameter) => (
              <div key={parameter.label} style={{ display: "contents" }}>
                <div style={{ fontWeight: 700 }}>{parameter.label}</div>
                <div>
                  <code>{String(parameter.value)}</code>
                </div>
              </div>
            ))}
          </div>
        </Section>

        {/* What it tests */}
        <Section title="What it tests">
          <ul
            style={{
              margin: 0,
              paddingLeft: 20,
              display: "grid",
              gap: 6,
            }}
          >
            {scenario.whatItTests.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </Section>

        {/* Expected reading */}
        <Section title="Expected reading">
          <ul
            style={{
              margin: 0,
              paddingLeft: 20,
              display: "grid",
              gap: 6,
            }}
          >
            {scenario.expectedReading.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </Section>

        {/* Simple explanation */}
        <Section title="Simple explanation">
          <div>{scenario.simpleExplanation}</div>
        </Section>
      </div>
    </div>
  );
}