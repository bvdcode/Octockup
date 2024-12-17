import IUserData from "./IUserData";
import { refreshAccessToken } from "../api/api";
import createRefresh from "react-auth-kit/createRefresh";

export const refresh = createRefresh<IUserData>({
  refreshApiCallback: async (token) => {
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
  interval: 1,
});
