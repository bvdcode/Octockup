import { useSignalR } from "./useSignalR";
import { BackupStatus } from "../types/api";
import { useSchedulesApi } from "../api/schedulesApi";
import { useCallback, useEffect, useState } from "react";
import type { ScheduleItem, ScheduleReport } from "../types/api";

interface SchedulesState {
  loading: boolean;
  error: string | null;
  deletingId: string | null;
  cancelingId: string | null;
  resettingId: string | null;
}

interface UseSchedulesReturn {
  items: ScheduleItem[];
  scheduleReports: Record<string, ScheduleReport>;
  state: SchedulesState;
  deleteSchedule: (id: string) => Promise<void>;
  cancelSchedule: (id: string) => Promise<void>;
  resetError: (id: string) => Promise<void>;
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
  });

  const [items, setItems] = useState<ScheduleItem[]>([]);
  const [scheduleReports, setScheduleReports] = useState<
    Record<string, ScheduleReport>
  >({});

  // Load schedules
  const refetchSchedules = useCallback(() => {
    api
      .list()
      .then((data) => {
        setItems(data);
        setState((prev) => ({
          ...prev,
          loading: false,
          error: null,
        }));
      })
      .catch((e) => {
        setState((prev) => ({
          ...prev,
          loading: false,
          error: e?.message || "Failed to load schedules",
        }));
      });
  }, [api]);

  useEffect(() => {
    let active = true;

    api
      .list()
      .then((data) => {
        if (!active) return;
        setItems(data);
        setState({
          loading: false,
          error: null,
          deletingId: null,
          cancelingId: null,
          resettingId: null,
        });
      })
      .catch((e) => {
        if (!active) return;
        setState({
          loading: false,
          error: e?.message || "Failed to load schedules",
          deletingId: null,
          cancelingId: null,
          resettingId: null,
        });
      });

    return () => {
      active = false;
    };
  }, [api]);

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
              setTimeout(() => refetchSchedules(), 500);
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
      // Try to reload; if it hits 401, global auth flow will handle it
      refetchSchedules();
    };

    const onClose = () => {
      refetchSchedules();
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

  return {
    items,
    scheduleReports,
    state,
    deleteSchedule,
    cancelSchedule,
    resetError,
  };
}
