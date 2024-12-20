import styles from "./ProgressBar.module.css";

export enum ProgressBarColor {
  Neutral = "#bfbfbf8a",
  Red = "#ff657c8a",
  Green = "#5aff748a",
  Yellow = "#feff668a",
}

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
