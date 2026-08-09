import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import SnapshotActionsCell from "./SnapshotActionsCell";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

describe("SnapshotActionsCell", () => {
  afterEach(cleanup);

  it("expands the four download actions to the left", async () => {
    const user = userEvent.setup();
    const onDownload = vi.fn();
    render(
      <SnapshotActionsCell
        downloadDisabled={false}
        deleting={false}
        onDownload={onDownload}
        onCopyLink={vi.fn()}
        onDelete={vi.fn()}
      />,
    );

    expect(
      screen.queryByRole("button", { name: "snapshots.downloadValidated" }),
    ).not.toBeInTheDocument();

    await user.click(
      screen.getByRole("button", { name: "snapshots.downloadOptions" }),
    );

    expect(
      screen.getByRole("button", { name: "snapshots.download" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "snapshots.downloadValidated" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "snapshots.copyLink" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "snapshots.copyValidatedLink" }),
    ).toBeInTheDocument();

    await user.click(
      screen.getByRole("button", { name: "snapshots.downloadValidated" }),
    );

    expect(onDownload).toHaveBeenCalledWith(true);
    await waitFor(() => {
      expect(
        screen.queryByRole("button", {
          name: "snapshots.downloadValidated",
        }),
      ).not.toBeInTheDocument();
    });
  });

  it("keeps snapshot deletion as a separate action", async () => {
    const user = userEvent.setup();
    const onDelete = vi.fn().mockResolvedValue(undefined);
    render(
      <SnapshotActionsCell
        downloadDisabled={false}
        deleting={false}
        onDownload={vi.fn()}
        onCopyLink={vi.fn()}
        onDelete={onDelete}
      />,
    );

    await user.click(
      screen.getByRole("button", { name: "snapshots.deleteTooltip" }),
    );

    expect(onDelete).toHaveBeenCalledOnce();
  });

  it("allows deleting an incomplete snapshot while downloads stay disabled", () => {
    render(
      <SnapshotActionsCell
        downloadDisabled
        deleting={false}
        onDownload={vi.fn()}
        onCopyLink={vi.fn()}
        onDelete={vi.fn()}
      />,
    );

    expect(
      screen.getByRole("button", { name: "snapshots.downloadOptions" }),
    ).toBeDisabled();
    expect(
      screen.getByRole("button", { name: "snapshots.deleteTooltip" }),
    ).toBeEnabled();
  });
});
