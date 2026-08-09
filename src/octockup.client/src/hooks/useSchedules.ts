import { useCallback, useEffect, useState } from "react";
import { isAxiosError } from "axios";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useSignalR } from "./useSignalR";
import { useSchedulesApi } from "../api/schedulesApi";
import { queryKeys } from "../query/queryKeys";
import {
  BackupStatus,
  type ScheduleItem,
  type ScheduleReport,
} from "../types/api";

const MAX_RETRY_DELAY_MS = 30_000;
const RETRY_BASE_DELAY_MS = 1_000;
const TERMINAL_REFRESH_DELAY_MS = 500;

interface SchedulesState {
  loading: boolean;
  error: string | null;
  deletingId: string | null;
  cancelingId: string | null;
  resettingId: string | null;
  cleaningUp: boolean;
}

interface ScheduleActionsState {
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

export function useSchedules(): UseSchedulesReturn {
  const api = useSchedulesApi();
  const queryClient = useQueryClient();
  const { connection, isConnected } = useSignalR("/api/v1/event-hub");
  const schedulesQuery = useQuery({
    queryKey: queryKeys.schedules,
    queryFn: () => api.list(),
    retry: (_failureCount, error) => {
      if (!isAxiosError(error)) {
        return true;
      }

      const status = error.response?.status;
      return status === undefined || status >= 500;
    },
    retryDelay: (failureCount) =>
      Math.min(
        MAX_RETRY_DELAY_MS,
        RETRY_BASE_DELAY_MS * Math.pow(2, failureCount),
      ),
  });
  const items = schedulesQuery.data ?? [];
  const refetchSchedules = schedulesQuery.refetch;
  const [actions, setActions] = useState<ScheduleActionsState>({
    deletingId: null,
    cancelingId: null,
    resettingId: null,
    cleaningUp: false,
  });
  const [scheduleReports, setScheduleReports] = useState<
    Record<string, ScheduleReport>
  >({});

  const updateSchedules = useCallback(
    (updater: (current: ScheduleItem[]) => ScheduleItem[]) => {
      queryClient.setQueryData<ScheduleItem[]>(
        queryKeys.schedules,
        (current) => updater(current ?? []),
      );
    },
    [queryClient],
  );

  useEffect(() => {
    if (!connection || !isConnected) {
      return;
    }

    void refetchSchedules();

    const handler = (report: ScheduleReport) => {
      setScheduleReports((current) => ({
        ...current,
        [report.scheduleId]: report,
      }));

      const currentSchedule = queryClient
        .getQueryData<ScheduleItem[]>(queryKeys.schedules)
        ?.find((item) => item.id === report.scheduleId);
      const reachedTerminalState =
        currentSchedule?.status === BackupStatus.Running &&
        report.status !== BackupStatus.Running;

      updateSchedules((current) =>
        current.map((item) =>
          item.id === report.scheduleId
            ? {
                ...item,
                status: report.status,
                errorMessage:
                  report.status === BackupStatus.Failed ? report.message : null,
                finishedAt:
                  report.status === BackupStatus.Running
                    ? null
                    : report.timestamp,
              }
            : item,
        ),
      );

      void queryClient.invalidateQueries({
        queryKey: queryKeys.backups,
        refetchType: "none",
      });

      if (reachedTerminalState) {
        window.setTimeout(() => {
          void queryClient.invalidateQueries({ queryKey: queryKeys.schedules });
        }, TERMINAL_REFRESH_DELAY_MS);
      }
    };

    connection.on("ScheduleReport", handler);
    return () => {
      connection.off("ScheduleReport", handler);
    };
  }, [
    connection,
    isConnected,
    queryClient,
    refetchSchedules,
    updateSchedules,
  ]);

  const invalidateBackups = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.backups });
  };

  const deleteSchedule = async (id: string): Promise<void> => {
    setActions((current) => ({ ...current, deletingId: id }));
    try {
      await api.delete(id);
      updateSchedules((current) =>
        current.filter((schedule) => schedule.id !== id),
      );
      await invalidateBackups();
    } finally {
      setActions((current) => ({ ...current, deletingId: null }));
    }
  };

  const cancelSchedule = async (id: string): Promise<void> => {
    setActions((current) => ({ ...current, cancelingId: id }));
    try {
      await api.cancel(id);
      updateSchedules((current) =>
        current.map((schedule) =>
          schedule.id === id
            ? { ...schedule, status: BackupStatus.Failed }
            : schedule,
        ),
      );
      await invalidateBackups();
    } finally {
      setActions((current) => ({ ...current, cancelingId: null }));
    }
  };

  const resetError = async (id: string): Promise<void> => {
    setActions((current) => ({ ...current, resettingId: id }));
    try {
      await api.resetError(id);
      updateSchedules((current) =>
        current.map((schedule) =>
          schedule.id === id
            ? {
                ...schedule,
                status: BackupStatus.Created,
                errorMessage: null,
              }
            : schedule,
        ),
      );
      await invalidateBackups();
    } finally {
      setActions((current) => ({ ...current, resettingId: null }));
    }
  };

  const cleanupCompletedSchedules = async (): Promise<void> => {
    setActions((current) => ({ ...current, cleaningUp: true }));
    try {
      const toDelete = items.filter(
        (item) =>
          item.interval === null &&
          (item.status === BackupStatus.Completed ||
            item.status === BackupStatus.Failed),
      );
      await Promise.all(toDelete.map((item) => api.delete(item.id)));
      const deletedIds = new Set(toDelete.map((item) => item.id));
      updateSchedules((current) =>
        current.filter((schedule) => !deletedIds.has(schedule.id)),
      );
      await invalidateBackups();
    } finally {
      setActions((current) => ({ ...current, cleaningUp: false }));
    }
  };

  return {
    items,
    scheduleReports,
    state: {
      loading: schedulesQuery.isPending,
      error: schedulesQuery.error?.message ?? null,
      ...actions,
    },
    deleteSchedule,
    cancelSchedule,
    resetError,
    cleanupCompletedSchedules,
  };
}
