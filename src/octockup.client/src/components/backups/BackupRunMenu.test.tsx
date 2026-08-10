import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { BackupRunMenu } from "./BackupRunMenu";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

describe("BackupRunMenu", () => {
  afterEach(cleanup);

  it("sets a quick recurring interval", async () => {
    const user = userEvent.setup();
    const onSetSchedule = vi.fn().mockResolvedValue(undefined);
    render(
      <BackupRunMenu
        disabled={false}
        intervalMinutes={null}
        loading={false}
        onDisableSchedule={vi.fn().mockResolvedValue(undefined)}
        onRunNow={vi.fn().mockResolvedValue(undefined)}
        onSetSchedule={onSetSchedule}
      />,
    );

    await user.click(
      screen.getByRole("button", { name: "backups.runOrSchedule" }),
    );
    await user.click(
      screen.getByRole("button", { name: "backups.schedule.quick.day" }),
    );

    expect(onSetSchedule).toHaveBeenCalledWith(1_440);
    await waitFor(() => {
      expect(
        screen.queryByRole("button", {
          name: "backups.schedule.quick.day",
        }),
      ).not.toBeInTheDocument();
    });
  });

  it("runs immediately without replacing the recurring interval", async () => {
    const user = userEvent.setup();
    const onRunNow = vi.fn().mockResolvedValue(undefined);
    const onSetSchedule = vi.fn().mockResolvedValue(undefined);
    render(
      <BackupRunMenu
        disabled={false}
        intervalMinutes={1_440}
        loading={false}
        onDisableSchedule={vi.fn().mockResolvedValue(undefined)}
        onRunNow={onRunNow}
        onSetSchedule={onSetSchedule}
      />,
    );

    await user.click(
      screen.getByRole("button", { name: "backups.runOrSchedule" }),
    );
    await user.click(
      screen.getByRole("button", { name: "backups.schedule.now" }),
    );

    expect(onRunNow).toHaveBeenCalledOnce();
    expect(onSetSchedule).not.toHaveBeenCalled();
  });

  it("offers disabling an existing recurring interval", async () => {
    const user = userEvent.setup();
    const onDisableSchedule = vi.fn().mockResolvedValue(undefined);
    render(
      <BackupRunMenu
        disabled={false}
        intervalMinutes={60}
        loading={false}
        onDisableSchedule={onDisableSchedule}
        onRunNow={vi.fn().mockResolvedValue(undefined)}
        onSetSchedule={vi.fn().mockResolvedValue(undefined)}
      />,
    );

    await user.click(
      screen.getByRole("button", { name: "backups.runOrSchedule" }),
    );
    await user.click(
      screen.getByRole("button", { name: "backups.schedule.off" }),
    );

    expect(onDisableSchedule).toHaveBeenCalledOnce();
  });
});
