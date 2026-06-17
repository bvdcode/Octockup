# Snapshot Archive Download Plan

- [x] Inspect current snapshot download, auth, and UI patterns.
- [x] Add a streaming stored ZIP64 archive writer with exact `Content-Length` calculation.
- [x] Add a snapshot archive endpoint that reuses the existing chunk restore pipeline.
- [x] Add focused unit tests for archive length calculation and readable ZIP output.
- [x] Add snapshot list UI actions for browser download and copying a tokenized archive link.
- [x] Run backend/frontend checks and record the result here.

## Decisions

- Archive format: `.zip`, stored entries only, no extra compression.
- Size reporting: calculate the full archive size before streaming and set `Content-Length`.
- Large file support: write ZIP64 metadata instead of relying on 32-bit ZIP fields.
- Link sharing: use the existing JWT `access_token` query pattern already used by file downloads.

## Verification

- `dotnet build src\Octockup.sln --no-restore`: passed.
- `dotnet test src\Octockup.Tests\Octockup.Tests.csproj --no-restore --filter "FullyQualifiedName~StoredZipArchiveWriterTests"`: passed.
- `npm run build`: passed with existing Vite/Rollup warnings.
- `npx eslint src/pages/Snapshots.tsx`: passed.
- Full `dotnet test` still fails on existing network-dependent IMAP/SFTP tests with `SocketException 10013`.
- Full `npm run lint` still fails on existing unrelated lint issues in `src/stores/snapshotsStore.ts` and `src/utils/dateUtils.ts`.
