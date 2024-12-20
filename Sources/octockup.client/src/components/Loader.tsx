import { useTranslation } from "react-i18next";

interface LoaderProps {
  text?: string;
}

const Loader: React.FC<LoaderProps> = ({ text }) => {
  const { t } = useTranslation();
  return <>{text ?? t("loader.loading")}</>;
};

export default Loader;
