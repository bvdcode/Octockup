import createRefresh from "react-auth-kit/createRefresh";

export const refresh = createRefresh({
  refreshApiCallback: async (token) => {
    // mock
    console.log("Refresh API Called", token);
    return {
      isSuccess: true,
      newAuthToken: "newAuthToken",
      newAuthTokenExpireIn: 10,
      newRefreshTokenExpiresIn: 60,
    };
  },
  interval: 60,
});
