import AxiosClient from "../../api/AxiosClient";
import {
  Box,
  Button,
  Card,
  CardContent,
  TextField,
  Typography,
} from "@mui/material";

const Profile: React.FC = () => {
  return (
    <Box sx={{ p: 2 }}>
      <Typography variant="h1" component="h1">
        Profile
      </Typography>
      <Card sx={{ my: 2, p: 2 }}>
        <CardContent>
          <TextField
            label="New Password"
            type="password"
            variant="outlined"
            color="primary"
            fullWidth
            sx={{ mb: 2 }}
          />
          <Button variant="contained" color="primary" fullWidth sx={{ mt: 2 }}>
            Change Password
          </Button>
        </CardContent>
      </Card>
      <Card sx={{ my: 2, p: 2 }}>
        <CardContent>
          <Button
            variant="contained"
            color="secondary"
            fullWidth
            onClick={() => AxiosClient.events.emit("logout")}
          >
            Logout
          </Button>
        </CardContent>
      </Card>
    </Box>
  );
};

export default Profile;
