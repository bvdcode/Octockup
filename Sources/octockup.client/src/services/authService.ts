import SHA512 from "crypto-js/sha512";

export const handleLogin = async (username: string, password: string) => {
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
    localStorage.setItem("authToken", data.accessToken);
    return true;
  } else {
    return false;
  }
};
