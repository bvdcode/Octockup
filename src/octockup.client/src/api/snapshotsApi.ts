import { useMemo } from "react";
import { useAxios } from "@bvdcode/react-kit";
import { SnapshotsApiClient } from "./snapshotsApiClient";

export function useSnapshotsApi(): SnapshotsApiClient {
  const axios = useAxios();
  return useMemo(() => new SnapshotsApiClient(() => axios), [axios]);
}
