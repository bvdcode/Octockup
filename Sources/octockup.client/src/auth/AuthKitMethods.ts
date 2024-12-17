import { refreshAccessToken } from "../api/api";
import IUserData from "./IUserData";
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
      newAuthTokenExpireIn: 10,
      newRefreshTokenExpiresIn: 60,
      newAuthToken: tokens.accessToken,
      newRefreshToken: tokens.refreshToken,
    };
  },
  interval: 60,
});
