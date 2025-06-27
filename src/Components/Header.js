import React from "react";
import {
  Box,
  Typography,
  Grid,
  Button,
  InputBase,
  Divider,
  TextField,
  InputAdornment,
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
            sx={{
              mt: 2,
              display: "flex",
              flexWrap: "wrap",
              justifyContent: "center",
              width: { xs: "100%", md: "70%" },
            }}
          >
            <Box
              component="form"
              sx={{
                display: "flex",
                alignItems: "center",
                width: "100%",
              }}
              // onSubmit={handleSearch}
            >
              <TextField
                size="small"
                fullWidth
                autoComplete="off"
                label="Sign Up it's free"
                // onChange={(e) => setSearchQuery(e.target.value)}
                sx={{
                  backgroundColor: "#FAFAFB",
                  borderRadius: "8px",
                  "& .MuiFormLabel-root": {
                    fontSize: "13px",
                    marginTop: "2px",
                    color: "#0399DF",
                    fontWeight: "bold",
                  },
                }}
                InputProps={{
                  endAdornment: (
                    <InputAdornment position="end">
                      <Button
                        color="primary"
                        sx={{
                          textTransform: "none",
                          fontWeight: "bold",
                          cursor: "pointer",
                        }}
                        aria-label="sign-up"
                      >
                        Sign Up
                      </Button>
                    </InputAdornment>
                  ),
                  sx: {
                    ".MuiOutlinedInput-notchedOutline": {
                      border: "2px solid #0399DF !important",
                      borderRadius: "8px",
                    },
                    "&:hover .MuiOutlinedInput-notchedOutline": {
                      border: "2px solid #0399DF !important",
                    },
                  },
                }}
              />
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
