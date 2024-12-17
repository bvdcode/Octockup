import SHA512 from "crypto-js/sha512";

export const login = async (username: string, password: string) => {
  const passwordHash = SHA512(password).toString();
  const response = await fetch("http://localhost:5112/api/v1/auth/login", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ username, passwordHash }),
  });

  if (response.ok) {
    const data = await response.json();
    return data;
  } else {
    throw new Error("Login failed");
  }
};
