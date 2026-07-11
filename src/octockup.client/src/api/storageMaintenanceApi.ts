import { useMemo } from "react";
import { useAxios } from "@bvdcode/react-kit";
import { StorageMaintenanceApiClient } from "./storageMaintenanceApiClient";

export function useStorageMaintenanceApi(): StorageMaintenanceApiClient {
  const axios = useAxios();
  return useMemo(() => new StorageMaintenanceApiClient(() => axios), [axios]);
}
