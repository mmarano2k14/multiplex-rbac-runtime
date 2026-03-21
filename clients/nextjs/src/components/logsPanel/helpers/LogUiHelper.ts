import { ConsoleLogEntry, HttpLogEntry, RealtimeLogEntry } from "@/lib/infrastructure/logs/inMemoryLogType";


export class LogUiHelper {
  public static getLogColor(log: ConsoleLogEntry): string {
    if (log.kind === "http") {
      const status = log.status ?? 0;

      if (status >= 200 && status < 300) return "#0f9d58";
      if (status >= 400 && status < 500) return "#f29900";
      if (status >= 500) return "#d93025";

      return "#1a73e8";
    }

    if (log.kind === "realtime") {
      const level = (log.level ?? "").toLowerCase();

      if (level === "error") return "#d93025";
      if (level === "warning") return "#f29900";

      return "#1a73e8";
    }

    return "#999";
  }

  public static isHttpLogEntry(log: ConsoleLogEntry): log is HttpLogEntry {
    return log.kind === "http";
  }

  public static isRealtimeLogEntry(log: ConsoleLogEntry): log is RealtimeLogEntry {
    return log.kind === "realtime";
  }

  public static isContextRotationLog(
    log: ConsoleLogEntry
  ): log is HttpLogEntry & { rotation: { from: string; to: string } } {
    return log.kind === "http" && log.rotation !== undefined;
  }

  public static isHttpErrorLogEntry(
    log: ConsoleLogEntry
  ): log is HttpLogEntry  {
    return log.kind === "http" && log.status !== undefined && log?.status >= 400;
  }

  public static isNearTop(element: HTMLDivElement, threshold = 40): boolean {
    return element.scrollTop <= threshold;
  }
}