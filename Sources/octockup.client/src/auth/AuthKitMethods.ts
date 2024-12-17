import { refreshAccessToken } from "../api/api";
import IUserData from "./IUserData";
import createRefresh from "react-auth-kit/createRefresh";

export const refresh = createRefresh<IUserData>({
  refreshApiCallback: async (token) => {
    console.log("calling refresh token");
    if (!token.refreshToken) {
      return {
        isSuccess: false,
        newAuthToken: "",
      };
    }

    const tokens = await refreshAccessToken(token.refreshToken);
    return {
      isSuccess: true,
      newAuthToken: tokens.accessToken,
      newRefreshToken: tokens.refreshToken,
    };
  },
  interval: 5,
});
