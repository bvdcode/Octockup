import { useEffect, useState } from "react";
import { BackupStatus } from "../types/api";
import type { ScheduleItem, ScheduleReport } from "../types/api";
import { useSchedulesApi } from "../api/schedulesApi";
import { useSignalR } from "./useSignalR";

interface SchedulesState {
  loading: boolean;
  error: string | null;
  deletingId: string | null;
  cancelingId: string | null;
}

interface UseSchedulesReturn {
  items: ScheduleItem[];
  scheduleReports: Record<string, ScheduleReport>;
  state: SchedulesState;
  deleteSchedule: (id: string) => Promise<void>;
  cancelSchedule: (id: string) => Promise<void>;
}

export function useSchedules(): UseSchedulesReturn {
  const api = useSchedulesApi();
  const { connection, isConnected } = useSignalR("/api/v1/event-hub");
  
  const [state, setState] = useState<SchedulesState>({
    loading: true,
    error: null,
    deletingId: null,
    cancelingId: null,
  });
  
  const [items, setItems] = useState<ScheduleItem[]>([]);
  const [scheduleReports, setScheduleReports] = useState<
    Record<string, ScheduleReport>
  >({});

  // Load schedules
  const refetchSchedules = () => {
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
  };

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
        });
      })
      .catch((e) => {
        if (!active) return;
        setState({
          loading: false,
          error: e?.message || "Failed to load schedules",
          deletingId: null,
          cancelingId: null,
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
            
            // Если статус изменился с Running на НЕ Running - делаем рефетч
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
  }, [connection, isConnected]);

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

  return {
    items,
    scheduleReports,
    state,
    deleteSchedule,
    cancelSchedule,
  };
}
