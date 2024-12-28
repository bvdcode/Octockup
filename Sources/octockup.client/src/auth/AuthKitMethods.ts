import { User } from "../api/types";
import { refreshAccessToken } from "../api/api";
import createRefresh from "react-auth-kit/createRefresh";

export const refresh = createRefresh<User>({
  interval: 300,
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
      newAuthUserState: tokens.user,
      newAuthToken: tokens.accessToken,
      newRefreshToken: tokens.refreshToken,
    };
  },
});
