import SHA512 from "crypto-js/sha512";

interface LoginRequest {
  username: string;
  passwordHash: string;
}

interface LoginResponse {
  accessToken: string;
  refreshToken: string;
}

export const login = async (
  username: string,
  password: string
): Promise<LoginResponse> => {
  const request = {
    username: username,
    passwordHash: SHA512(password).toString(),
  } as LoginRequest;
  const response = await fetch("http://localhost:5112/api/v1/auth/login", {
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
