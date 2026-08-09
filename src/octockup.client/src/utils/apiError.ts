import { isAxiosError } from "axios";
import type { ApiErrorResponse } from "../types/auth";

export function getApiErrorMessage(error: Error, fallback: string): string {
  if (isAxiosError<ApiErrorResponse>(error)) {
    return (
      error.response?.data.detail ?? error.response?.data.message ?? fallback
    );
  }

  return error.message || fallback;
}
