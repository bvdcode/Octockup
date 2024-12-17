interface NavButton {
  path: string;
  icon: JSX.Element;
}

interface NavBarProps {
  buttons: NavButton[];
}

const NavBar: React.FC<NavBarProps> = (props) => {
  return (
    <>
      {props.buttons.map((button, index) => (
        <button key={index}>{button.icon}</button>
      ))}
    </>
  );
};

export default NavBar;
