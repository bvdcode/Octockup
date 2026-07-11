import { useMemo } from "react";
import { useAxios } from "@bvdcode/react-kit";
import { BackupsApiClient } from "./backupsApiClient";

export function useBackupsApi(): BackupsApiClient {
  const axios = useAxios();
  return useMemo(() => new BackupsApiClient(() => axios), [axios]);
}
