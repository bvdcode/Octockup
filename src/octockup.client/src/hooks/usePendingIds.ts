import { useCallback, useState } from "react";

interface PendingIds {
  has: (id: string) => boolean;
  run: <T>(id: string, action: () => Promise<T>) => Promise<T>;
}

export function usePendingIds(): PendingIds {
  const [ids, setIds] = useState<ReadonlySet<string>>(() => new Set());

  const run = useCallback(
    async <T,>(id: string, action: () => Promise<T>): Promise<T> => {
      setIds((current) => {
        const next = new Set(current);
        next.add(id);
        return next;
      });
      try {
        return await action();
      } finally {
        setIds((current) => {
          const next = new Set(current);
          next.delete(id);
          return next;
        });
      }
    },
    [],
  );

  return {
    has: (id) => ids.has(id),
    run,
  };
}
