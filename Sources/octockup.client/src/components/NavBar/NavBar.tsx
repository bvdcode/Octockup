import styles from "./NavBar.module.css";
import { useNavigate } from "react-router-dom";

interface NavButton {
  path: string;
  icon: JSX.Element;
}

interface NavBarProps {
  buttons: NavButton[];
}

const NavBar: React.FC<NavBarProps> = (props) => {
  const navigate = useNavigate();

  return (
    <div className={styles.navBarContainer}>
      {props.buttons.map((button, index) => (
        <button key={index} onClick={() => navigate(button.path)}>
          {button.icon}
        </button>
      ))}
    </div>
  );
};

export default NavBar;
