import React from "react";
import Alert from "@mui/material/Alert";
import Stack from "@mui/material/Stack";

const Alerts = ({ severity, title }) => {
  return (
    <Stack
      sx={{ width: "100%", marginBottom: "20px" }}
      variant="filled"
      spacing={2}
    >
      <Alert severity={severity}>{title}</Alert>
    </Stack>
  );
};

export default Alerts;
