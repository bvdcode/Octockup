import axios from "axios";
import type { LoginCredentials, TokenPair } from "@bvdcode/react-kit";
import { toCookieSession } from "../utils/authSession";

const publicApi = axios.create();

export async function loginWithPassword(
  credentials: LoginCredentials,
): Promise<TokenPair> {
  const response = await publicApi.post<TokenPair>(
    "/api/v1/auth/login",
    credentials,
  );
  return toCookieSession(response.data);
}
