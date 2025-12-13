import { useSignalR } from "./useSignalR";
import { BackupStatus } from "../types/api";
import { useSchedulesApi } from "../api/schedulesApi";
import { useCallback, useEffect, useRef, useState } from "react";
import type { ScheduleItem, ScheduleReport } from "../types/api";

interface SchedulesState {
  loading: boolean;
  error: string | null;
  deletingId: string | null;
  cancelingId: string | null;
  resettingId: string | null;
  cleaningUp: boolean;
}

interface UseSchedulesReturn {
  items: ScheduleItem[];
  scheduleReports: Record<string, ScheduleReport>;
  state: SchedulesState;
  deleteSchedule: (id: string) => Promise<void>;
  cancelSchedule: (id: string) => Promise<void>;
  resetError: (id: string) => Promise<void>;
  cleanupCompletedSchedules: () => Promise<void>;
}

function getHttpStatus(e: unknown): number | null {
  const anyErr = e as { response?: { status?: number } };
  return anyErr?.response?.status ?? null;
}

export function useSchedules(): UseSchedulesReturn {
  const api = useSchedulesApi();
  const { connection, isConnected } = useSignalR("/api/v1/event-hub");

  const [state, setState] = useState<SchedulesState>({
    loading: true,
    error: null,
    deletingId: null,
    cancelingId: null,
    resettingId: null,
    cleaningUp: false,
  });

  const [items, setItems] = useState<ScheduleItem[]>([]);
  const [scheduleReports, setScheduleReports] = useState<
    Record<string, ScheduleReport>
  >({});

  const retryAttemptRef = useRef(0);
  const retryTimerRef = useRef<number | null>(null);
  const refetchRef = useRef<((silentOn5xx?: boolean) => void) | null>(null);

  const scheduleRetry = useCallback(() => {
    const attempt = retryAttemptRef.current + 1;
    retryAttemptRef.current = attempt;
    const delay = Math.min(30000, 1000 * Math.pow(2, attempt - 1)); // 1s,2s,4s,8s,16s,30s cap
    if (retryTimerRef.current) {
      clearTimeout(retryTimerRef.current);
    }
    retryTimerRef.current = setTimeout(() => {
      refetchRef.current?.(true);
    }, delay) as unknown as number;
  }, []);

  // Load schedules
  const refetchSchedules = useCallback(
    (silentOn5xx = false) => {
      api
        .list()
        .then((data) => {
          setItems(data);
          retryAttemptRef.current = 0;
          if (retryTimerRef.current) {
            clearTimeout(retryTimerRef.current);
            retryTimerRef.current = null;
          }
          setState((prev) => ({
            ...prev,
            loading: false,
            error: null,
            cleaningUp: false,
          }));
        })
        .catch((e) => {
          const status = getHttpStatus(e);
          if (silentOn5xx && (status === null || status >= 500)) {
            // keep existing data on screen and retry silently in background
            scheduleRetry();
            return;
          }

          setState((prev) => {
            // If we already have items, don't show 5xx errors - they're transient
            if (items.length > 0 && (status === null || status >= 500)) {
              // Silent fail, keep existing data
              return prev;
            }
            // If we already have items but it's a client error (4xx), show it
            if (items.length > 0) {
              return {
                ...prev,
                error: e?.message || "Failed to refresh schedules",
              };
            }
            // If no items yet and it's 5xx, retry silently
            if (status === null || status >= 500) {
              scheduleRetry();
              return prev;
            }
            // If no items and client error, show it
            return {
              ...prev,
              loading: false,
              error: e?.message || "Failed to load schedules",
            };
          });
        });
    },
    [api, scheduleRetry, items.length],
  );

  refetchRef.current = refetchSchedules;

  useEffect(() => {
    let active = true;

    api
      .list()
      .then((data) => {
        if (!active) return;
        setItems(data);
        retryAttemptRef.current = 0;
        setState({
          loading: false,
          error: null,
          deletingId: null,
          cancelingId: null,
          resettingId: null,
          cleaningUp: false,
        });
      })
      .catch((e) => {
        if (!active) return;
        const status = getHttpStatus(e);
        if (status === null || status >= 500) {
          // transient: keep loading spinner only on first mount, then retry
          scheduleRetry();
          return;
        }
        setState({
          loading: false,
          error: e?.message || "Failed to load schedules",
          deletingId: null,
          cancelingId: null,
          resettingId: null,
          cleaningUp: false,
        });
      });

    return () => {
      active = false;
      if (retryTimerRef.current) {
        clearTimeout(retryTimerRef.current);
        retryTimerRef.current = null;
      }
    };
  }, [api, scheduleRetry]);

  // WebSocket listener for schedule reports
  useEffect(() => {
    if (!connection || !isConnected) return;

    const handler = (report: ScheduleReport) => {
      setScheduleReports((prev) => ({
        ...prev,
        [report.scheduleId]: report,
      }));

      setItems((prev) => {
        const updated = prev.map((item) => {
          if (item.id === report.scheduleId) {
            const wasRunning = item.status === BackupStatus.Running;
            const isNowNotRunning = report.status !== BackupStatus.Running;

            if (wasRunning && isNowNotRunning) {
              setTimeout(() => refetchSchedules(true), 500);
            }

            return { ...item, status: report.status };
          }
          return item;
        });
        return updated;
      });
    };

    connection.on("ScheduleReport", handler);

    return () => {
      connection.off("ScheduleReport", handler);
    };
  }, [connection, isConnected, refetchSchedules]);

  // Reload schedules on connection errors/reconnect attempts
  useEffect(() => {
    if (!connection) return;

    const onReconnecting = () => {
      // Try to reload silently; if it hits 401, global auth flow will handle it
      refetchSchedules(true);
    };

    const onClose = () => {
      refetchSchedules(true);
    };

    connection.onreconnecting(onReconnecting);
    connection.onclose(onClose);

    return () => {
      connection.off(
        "reconnecting",
        onReconnecting as unknown as (...args: unknown[]) => void,
      );
      connection.off(
        "close",
        onClose as unknown as (...args: unknown[]) => void,
      );
    };
  }, [connection, refetchSchedules]);

  const deleteSchedule = async (id: string): Promise<void> => {
    setState((s) => ({ ...s, deletingId: id }));
    try {
      await api.delete(id);
      setItems((prev) => prev.filter((x) => x.id !== id));
    } finally {
      setState((s) => ({ ...s, deletingId: null }));
    }
  };

  const cancelSchedule = async (id: string): Promise<void> => {
    setState((s) => ({ ...s, cancelingId: id }));
    try {
      await api.cancel(id);
      setItems((prev) =>
        prev.map((x) =>
          x.id === id ? { ...x, status: BackupStatus.Failed } : x,
        ),
      );
    } finally {
      setState((s) => ({ ...s, cancelingId: null }));
    }
  };

  const resetError = async (id: string): Promise<void> => {
    setState((s) => ({ ...s, resettingId: id }));
    try {
      await api.resetError(id);
      setItems((prev) =>
        prev.map((x) =>
          x.id === id ? { ...x, status: BackupStatus.Created, errorMessage: null } : x,
        ),
      );
    } finally {
      setState((s) => ({ ...s, resettingId: null }));
    }
  };

  const cleanupCompletedSchedules = async (): Promise<void> => {
    setState((s) => ({ ...s, cleaningUp: true }));
    try {
      const toDelete = items.filter(
        (item) =>
          item.interval === null &&
          (item.status === BackupStatus.Completed ||
            item.status === BackupStatus.Failed),
      );

      await Promise.all(toDelete.map((item) => api.delete(item.id)));

      setItems((prev) =>
        prev.filter(
          (x) =>
            !(x.interval === null &&
              (x.status === BackupStatus.Completed ||
                x.status === BackupStatus.Failed)),
        ),
      );
    } finally {
      setState((s) => ({ ...s, cleaningUp: false }));
    }
  };

  return {
    items,
    scheduleReports,
    state,
    deleteSchedule,
    cancelSchedule,
    resetError,
    cleanupCompletedSchedules,
  };
}
