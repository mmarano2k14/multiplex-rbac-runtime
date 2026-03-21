import { RealtimeLogEntry } from "@/lib/infrastructure/logs/inMemoryLogType";
import { LogBadge } from "../LogsPanelType";

export class RealtimeLogHelper {
  public static getLevelColor(level?: string): string {
    if (!level) {
      return "#777";
    }

    switch (level.toLowerCase()) {
      case "debug":
        return "#5f6368";

      case "information":
      case "info":
        return "#1a73e8";

      case "warning":
        return "#f29900";

      case "error":
        return "#d93025";

      case "critical":
        return "#8b0000";

      default:
        return "#777";
    }
  }

  public static getBadges(log: RealtimeLogEntry): LogBadge[] {
    const badges: LogBadge[] = [
      {
        label: "REALTIME",
        color: "#1f1f1f",
        background: "#efe",
      },
    ];

    const eventName = (log.eventName ?? "").toLowerCase();
    const message = (log.message ?? "").toLowerCase();

    if (eventName.includes("context-rotated") || message.includes("rotated")) {
      badges.push({
        label: "CONTEXT ROTATED",
        color: "#b06000",
        background: "#fff4e5",
      });
    }

    if (eventName.includes("runtime-log")) {
      badges.push({
        label: "RUNTIME",
        color: "#0b57d0",
        background: "#e8f0fe",
      });
    }

    if ((log.level ?? "").toLowerCase() === "warning") {
      badges.push({
        label: "WARNING",
        color: "#b06000",
        background: "#fff4e5",
      });
    }

    if ((log.level ?? "").toLowerCase() === "error") {
      badges.push({
        label: "ERROR",
        color: "#d93025",
        background: "#fce8e6",
      });
    }

    return badges;
  }
}