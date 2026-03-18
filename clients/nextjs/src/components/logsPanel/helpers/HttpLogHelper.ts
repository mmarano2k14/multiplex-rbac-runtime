import { HttpLogEntry } from "@/lib/logs/inMemoryLogType";
import { LogBadge } from "../LogsPanelType";

export class HttpLogHelper {
  public static getBadges(log: HttpLogEntry): LogBadge[] {
    const badges: LogBadge[] = [
      {
        label: "HTTP",
        color: "#1f1f1f",
        background: "#eef",
      },
    ];

    const path = (log.path ?? "").toLowerCase();
    const name = (log.name ?? "").toLowerCase();

    if (path.includes("refund") || name.includes("refund")) {
      badges.push({
        label: "REFUND",
        color: "#8a1c7c",
        background: "#f7e8f5",
      });
    }

    if (path.includes("invoice") || name.includes("read")) {
      badges.push({
        label: "READ",
        color: "#0b57d0",
        background: "#e8f0fe",
      });
    }

    if (path.includes("login") || name.includes("login")) {
      badges.push({
        label: "LOGIN",
        color: "#0f9d58",
        background: "#e6f4ea",
      });
    }

    if (log.rotation) {
      badges.push({
        label: "CONTEXT ROTATED",
        color: "#b06000",
        background: "#fff4e5",
      });
    }

    if (log.status === 401) {
      badges.push({
        label: "UNAUTHORIZED",
        color: "#b06000",
        background: "#fff4e5",
      });
    }

    if (log.status === 403) {
      badges.push({
        label: "FORBIDDEN",
        color: "#d93025",
        background: "#fce8e6",
      });
    }

    if (typeof log.status === "number" && log.status >= 500) {
      badges.push({
        label: "SERVER ERROR",
        color: "#d93025",
        background: "#fce8e6",
      });
    }

    return badges;
  }

  public static getStatusColor(status?: number): string {
    if (typeof status !== "number") {
      return "#777";
    }

    if (status >= 200 && status < 300) return "#0f9d58";
    if (status >= 300 && status < 400) return "#1a73e8";
    if (status >= 400 && status < 500) return "#f29900";
    if (status >= 500) return "#d93025";

    return "#777";
  }
}