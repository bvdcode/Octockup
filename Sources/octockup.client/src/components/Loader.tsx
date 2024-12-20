import { CircularProgress } from "@mui/material";
import { useTranslation } from "react-i18next";

interface LoaderProps {
  text?: string;
  progress?: number;
}

const Loader: React.FC<LoaderProps> = ({ text, progress }) => {
  const { t } = useTranslation();
  return (
    <div
      style={{
        position: "absolute",
        backgroundColor: "rgba(50, 50, 50, 0.95)",
        zIndex: 1000,
        width: "100%",
        height: "100%",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        gap: "1rem",
      }}
    >
      <CircularProgress value={progress} />
      {text ?? t("loader.loading")}
    </div>
  );
};

export default Loader;
