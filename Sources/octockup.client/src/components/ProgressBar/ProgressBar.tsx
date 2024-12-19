import styles from "./ProgressBar.module.css";

interface ProgressBarProps {
  value: number;
  error?: boolean;
}

const ProgressBar: React.FC<ProgressBarProps> = ({
  value: progress,
  error,
}) => {
  const percentage = progress * 100;
  return (
    <div className={styles.progressBarContainer}>
      <div
        className={styles.progressBar}
        role="progressbar"
        style={{
          width: `${percentage}%`,
          backgroundColor: error ? "#ff5959" : "#2fad2f",
        }}
        aria-valuenow={percentage}
        aria-valuemin={0}
        aria-valuemax={100}
      ></div>
    </div>
  );
};

export default ProgressBar;
