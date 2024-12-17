import SHA512 from "crypto-js/sha512";
import { API_BASE_URL } from "../config";
import { LoginRequest, LoginResponse } from "./types";

export const login = async (
  username: string,
  password: string
): Promise<LoginResponse> => {
  const request = {
    username: username,
    passwordHash: SHA512(password).toString(),
  } as LoginRequest;
  const response = await fetch(`${API_BASE_URL}/auth/login`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });

  if (response.ok) {
    const data: LoginResponse = await response.json();
    return data;
  } else {
    throw new Error("Login failed");
  }
};
