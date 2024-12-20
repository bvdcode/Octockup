import { CreateJobRequest } from "../api/types";

export type Action =
  | { type: "SET_PROVIDER"; payload: string }
  | { type: "SET_SETTINGS"; payload: { key: string; value: string } }
  | { type: "SET_JOB_NAME"; payload: string }
  | { type: "SET_INTERVAL"; payload: number }
  | { type: "SET_NOTIFICATIONS"; payload: boolean }
  | { type: "RESET" };

export const initialState: CreateJobRequest = {
  provider: "",
  settings: {},
  jobName: "",
  interval: 0,
  notifications: false,
};

export const reducer = (
  state: CreateJobRequest,
  action: Action
): CreateJobRequest => {
  switch (action.type) {
    case "SET_PROVIDER":
      return { ...state, provider: action.payload, settings: {} };
    case "SET_SETTINGS":
      return {
        ...state,
        settings: {
          ...state.settings,
          [action.payload.key]: action.payload.value,
        },
      };
    case "SET_JOB_NAME":
      return { ...state, jobName: action.payload };
    case "SET_INTERVAL":
      return { ...state, interval: action.payload };
    case "SET_NOTIFICATIONS":
      return { ...state, notifications: action.payload };
    case "RESET":
      return initialState;
    default:
      return state;
  }
};
