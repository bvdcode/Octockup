import styles from "./ProgressBar.module.css";
import { ProgressBarColor } from "./ProgressBarColor";

interface ProgressBarProps {
  value: number;
  color?: ProgressBarColor;
}

const ProgressBar: React.FC<ProgressBarProps> = (props) => {
  const percentage = props.value * 100;
  return (
    <div className={styles.progressBarContainer}>
      <div
        className={styles.progressBar}
        role="progressbar"
        style={{
          width: `${percentage}%`,
          backgroundColor: props.color ?? ProgressBarColor.Neutral,
        }}
        aria-valuenow={percentage}
        aria-valuemin={0}
        aria-valuemax={100}
      ></div>
    </div>
  );
};

export default ProgressBar;
