import axios, {
  AxiosHeaders,
  type InternalAxiosRequestConfig,
} from "axios";
import { describe, expect, it } from "vitest";
import { SnapshotsApiClient } from "./snapshotsApiClient";

describe("SnapshotsApiClient", () => {
  it("uses cursor paging and bounds archive job queries to visible snapshots", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    const axiosInstance = axios.create({
      adapter: async (config) => {
        requests.push(config);
        return {
          data:
            config.url === "/api/v1/snapshots"
              ? {
                  items: [],
                  nextCursor: "next",
                  hasNextPage: true,
                  totalCount: 500,
                }
              : [],
          status: 200,
          statusText: "OK",
          headers: new AxiosHeaders(),
          config,
        };
      },
    });
    const client = new SnapshotsApiClient(() => axiosInstance);

    const page = await client.listByBackup("backup", {
      pageSize: 50,
      cursor: "cursor",
    });
    await client.listArchiveJobs(["snapshot-1", "snapshot-2"]);

    expect(page.totalCount).toBe(500);
    expect(requests).toHaveLength(2);
    expect(requests[0]?.params).toEqual({
      backupId: "backup",
      pageSize: 50,
      cursor: "cursor",
    });
    expect(requests[1]?.url).toBe("/api/v1/snapshot-archive-jobs/query");
    expect(requests[1]?.method).toBe("post");
    expect(requests[1]?.data).toBe(
      '{"snapshotIds":["snapshot-1","snapshot-2"]}',
    );
  });
});
