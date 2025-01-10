import * as React from "react";
import Button from "@mui/material/Button";
import Dialog from "@mui/material/Dialog";
import DialogTitle from "@mui/material/DialogTitle";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogContentText from "@mui/material/DialogContentText";

export interface CustomDialogProps {
  title: string;
  content: string;
  cancelText: string;
  confirmText: string;
  doubleCheck?: boolean;
  onCancel?: () => void;
  onConfirm?: () => void;
  children: React.ReactNode;
}

export default function CustomDialog(props: CustomDialogProps) {
  const [open, setOpen] = React.useState(false);
  const [counter, setCounter] = React.useState(0);

  const handleClickOpen = () => {
    setOpen(true);
  };

  const handleClose = () => {
    setOpen(false);
  };

  const handleCancel = () => {
    setCounter(0);
    if (props.onCancel) {
      props.onCancel();
    }
    setOpen(false);
  };

  const handleConfirm = () => {
    if (props.doubleCheck) {
      setCounter(counter + 1);
      if (counter === 0) {
        return;
      }
    }
    if (props.onConfirm) {
      props.onConfirm();
    }
    setOpen(false);
  };

  return (
    <React.Fragment>
      <div onClick={handleClickOpen}>{props.children}</div>
      <Dialog
        open={open}
        onClose={handleClose}
        aria-labelledby="alert-dialog-title"
        aria-describedby="alert-dialog-description"
      >
        <DialogTitle id="alert-dialog-title">
          {props.title ?? "CustomDialog.title"}
        </DialogTitle>
        <DialogContent>
          <DialogContentText id="alert-dialog-description">
            {props.content ?? "CustomDialog.content"}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleCancel} color="primary">
            {props.cancelText ?? "CustomDialog.cancelText"}
          </Button>
          <Button
            onClick={handleConfirm}
            autoFocus
            color={counter === 0 ? "primary" : "error"}
          >
            {props.confirmText ?? "CustomDialog.confirmText"}
          </Button>
        </DialogActions>
      </Dialog>
    </React.Fragment>
  );
}
