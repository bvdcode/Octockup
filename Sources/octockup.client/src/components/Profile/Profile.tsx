import styles from "./Profile.module.css";
import AxiosClient from "../../api/AxiosClient";

const Profile: React.FC = () => {
  return (
    <div className={styles.profileContainer}>
      <h1>Profile</h1>
      <button
        className={styles.logoutButton}
        onClick={() => AxiosClient.events.emit("logout")}
      >
        Logout
      </button>
    </div>
  );
};

export default Profile;
