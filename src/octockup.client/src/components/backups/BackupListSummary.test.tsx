import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { BackupListSummary } from "./BackupListSummary";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

describe("BackupListSummary", () => {
  it("renders list statistics in a footer", () => {
    render(
      <BackupListSummary
        backupCount={22}
        issueCount={2}
        logicalSize={1024}
        runningCount={1}
      />,
    );

    const footer = screen.getByRole("contentinfo");
    expect(footer).toHaveTextContent("backups.summary.backups");
    expect(footer).toHaveTextContent("backups.summary.logicalSize");
    expect(footer).toHaveTextContent("backups.summary.running");
    expect(footer).toHaveTextContent("backups.summary.issues");
  });
});
