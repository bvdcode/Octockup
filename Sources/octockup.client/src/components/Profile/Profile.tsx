import styles from "./Profile.module.css";
import AxiosClient from "../../api/AxiosClient";
import { Button, TextField } from "@mui/material";

const Profile: React.FC = () => {
  return (
    <div className={styles.profileContainer}>
      <h1>Profile</h1>
      <div className={styles.changePasswordContainer}>
        <TextField
          className={styles.passwordField}
          label="New Password"
          type="password"
          variant="outlined"
          color="primary"
        />
      </div>
      <Button
        className={styles.logoutButton}
        onClick={() => AxiosClient.events.emit("logout")}
      >
        Logout
      </Button>
    </div>
  );
};

export default Profile;
