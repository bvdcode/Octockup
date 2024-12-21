import { CreateJobRequest } from "../api/types";

export type Action =
  | { type: "SET_PROVIDER"; payload: string }
  | { type: "SET_SETTINGS"; payload: { key: string; value: string } }
  | { type: "SET_JOB_NAME"; payload: string }
  | { type: "SET_INTERVAL"; payload: number }
  | { type: "SET_NOTIFICATIONS"; payload: boolean }
  | { type: "SET_START_AT"; payload: Date }
  | { type: "RESET" };

export const initialState: CreateJobRequest = {
  provider: "",
  parameters: {},
  name: "",
  interval: 0,
  isNotificationEnabled: false,
  startAt: null,
};

export const reducer = (
  state: CreateJobRequest,
  action: Action
): CreateJobRequest => {
  switch (action.type) {
    case "SET_PROVIDER":
      return { ...state, provider: action.payload, parameters: {} };
    case "SET_SETTINGS":
      return {
        ...state,
        parameters: {
          ...state.parameters,
          [action.payload.key]: action.payload.value,
        },
      };
    case "SET_JOB_NAME":
      return { ...state, name: action.payload };
    case "SET_INTERVAL":
      return { ...state, interval: action.payload };
    case "SET_NOTIFICATIONS":
      return { ...state, isNotificationEnabled: action.payload };
    case "SET_START_AT":
      return { ...state, startAt: action.payload };
    case "RESET":
      return initialState;
    default:
      return state;
  }
};
