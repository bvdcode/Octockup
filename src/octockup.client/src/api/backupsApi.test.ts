import axios, {
  AxiosHeaders,
  type InternalAxiosRequestConfig,
} from "axios";
import { describe, expect, it } from "vitest";
import { BackupsApiClient } from "./backupsApiClient";

describe("BackupsApiClient", () => {
  it("uploads server backups as a raw binary stream", async () => {
    const capturedRequests: InternalAxiosRequestConfig[] = [];
    const axiosInstance = axios.create({
      adapter: async (config) => {
        capturedRequests.push(config);
        return {
          data: { message: "queued" },
          status: 200,
          statusText: "OK",
          headers: new AxiosHeaders(),
          config,
        };
      },
    });
    const client = new BackupsApiClient(() => axiosInstance);
    const file = new File(["server-backup"], "backup.oct");

    const result = await client.importServerBackup(file);

    expect(result).toEqual({ message: "queued" });
    expect(capturedRequests).toHaveLength(1);
    const capturedRequest = capturedRequests[0];
    if (!capturedRequest) {
      throw new Error("Axios adapter did not receive the upload request.");
    }
    expect(capturedRequest.url).toBe("/api/v1/backups/server/import");
    expect(capturedRequest.data).toBe(file);
    expect(capturedRequest.headers.get("Content-Type")).toBe(
      "application/octet-stream",
    );
  });
});
