import React from "react";
import {
  Box,
  Typography,
  Grid,
  Button,
  TextField,
  Paper,
  IconButton,
  InputBase,
  Divider,
} from "@mui/material";
import header_photo from "../Pictures/header_pict.png";

const Header = () => {
  return (
    <Box
      p={5}
      sx={{
        background: "#FFF",
        background:
          "linear-gradient(350deg,rgba(255, 255, 255, 1) 56%, rgba(216, 246, 255, 1) 80%)",
      }}
    >
      <Grid container spacing={2}>
        <Grid
          size={{ xs: 12, md: 5 }}
          sx={{
            display: "flex",
            flexDirection: "column",
            justifyContent: "center",
            alignItems: "center",
            textAlign: "center",
          }}
        >
          <Typography
            color="#709CFF"
            fontWeight="bold"
            variant="h4"
            gutterBottom
          >
            Welcome to Abbisum
          </Typography>
          <Typography variant="body1">
            Empower Your Business with Modern Web & Marketing Solutions At
            Abbsium, we help businesses grow through the country cutting-edge
            web development and digital marketing strategies. From stunning
            websites to results-driven campaigns — we build your digital
            success.
          </Typography>
          <Box
            sx={{ mt: 2 }}
            display="flex"
            flexWrap="wrap"
            justifyContent="center"
          >
            <Box
              component="form"
              sx={{
                p: "2px 4px",
                display: "flex",
                alignItems: "center",
                width: 400,
                border: "0.5px solid #0399DF",
                borderRadius: "8px",
              }}
            >
              <InputBase
                sx={{ ml: 1, flex: 1 }}
                placeholder="Sign Up - It's free"
                inputProps={{ "aria-label": "search google maps" }}
              />
              <Divider sx={{ height: 28, m: 0.5 }} orientation="vertical" />
              <Button
                color="primary"
                sx={{
                  p: "10px",
                  textTransform: "none",
                  fontWeight: "bold",
                  cursor: "pointer",
                }}
                aria-label="directions"
              >
                Sign Up
              </Button>
            </Box>
          </Box>
        </Grid>
        <Grid size={{ xs: 12, md: 7 }}>
          <Box
            sx={{
              backgroundRepeat: "no-repeat",
              backgroundSize: "cover",
              textAlign: "center",
              justifyContent: "center",
              marginTop: "25px",
              objectFit: "contain",
              display: "block",
            }}
            width="100%"
            height="100%"
            alt="Persona trabajando"
            component="img"
            src={header_photo}
          />
        </Grid>
      </Grid>
    </Box>
  );
};

export default Header;
