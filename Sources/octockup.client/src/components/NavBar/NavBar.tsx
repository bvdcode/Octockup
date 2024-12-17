interface NavBarProps {
  iLikeTypeScript: boolean;
}

const NavBar: React.FC<NavBarProps> = (props) => {
  return <>I like TypeScript: {props.iLikeTypeScript.toString()}</>;
};

export default NavBar;
