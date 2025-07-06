// Footer.jsx
import React from "react";
import { Box, Grid, Typography, IconButton, Divider } from "@mui/material";
import AbbsiumLogo from "../Pictures/abbsium192.png";
import { Link as RouterLink } from "react-router-dom";

const creditCards = [
  "https://img.icons8.com/color/48/000000/visa.png",
  "https://img.icons8.com/color/48/000000/mastercard-logo.png",
  "https://img.icons8.com/color/48/000000/amex.png",
  "https://img.icons8.com/color/48/000000/paypal.png",
];

const socialMedia = [
  "https://img.icons8.com/?size=100&id=118497&format=png&color=000000",
  "https://img.icons8.com/?size=100&id=BrU2BBoRXiWq&format=png&color=000000",
  "https://img.icons8.com/?size=100&id=cSHiAiy2tBcA&format=png&color=000000",
  "https://img.icons8.com/?size=100&id=19318&format=png&color=000000",
];

const Footer = () => {
  return (
    <Box
      component="footer"
      sx={{
        background:
          "linear-gradient(165deg,rgba(255, 255, 255, 1) 56%, rgba(216, 246, 255, 1) 80%)",
        pt: 4,
        px: { xs: 3, md: 10 },
        borderTop: "1px solid #E0E0E0",
        mt: "auto", // clave para empujar al fondo
      }}
    >
      <Box textAlign="center" mb={8}>
        <Typography color="#709CFF" variant="h5" fontWeight={600} gutterBottom>
          Want to partner with us?
        </Typography>
        <Typography variant="body1" mb={2}>
          If you're interested in exploring a partnership with us and would like
          to learn more about how we can collaborate, our dedicated advisors are
          here to guide you through every step. We’re excited to share
          opportunities, answer your questions, and help bring your vision to
          life.
        </Typography>
      </Box>

      <Grid container spacing={4} justifyContent="space-between">
        <Grid
          size={{ xs: 4, md: 4, lg: 2 }}
          display="flex"
          justifyContent="center"
          alignItems="center"
        >
          <Box component="img" src={AbbsiumLogo} height="64px" />
        </Grid>

        <Grid size={{ xs: 4, md: 4, lg: 2 }}>
          <Typography variant="subtitle2" fontWeight={600} gutterBottom>
            Contact
          </Typography>
          <Typography variant="body2">+1 (904) 852 3178</Typography>
        </Grid>

        <Grid size={{ xs: 4, md: 4, lg: 2 }}>
          <Typography variant="subtitle2" fontWeight={600} gutterBottom>
            Support
          </Typography>
          <Typography variant="body2">Support Request</Typography>
          <Typography variant="body2">Contact</Typography>
        </Grid>

        <Grid size={{ xs: 6, md: 6, lg: 3 }} textAlign="center">
          <Typography variant="subtitle2" fontWeight={600} gutterBottom>
            Follow Me
          </Typography>
          {socialMedia.map((item, index) => (
            <IconButton size="small" key={index}>
              <Box component="img" sx={{ height: 28 }} src={item} />
            </IconButton>
          ))}
        </Grid>

        <Grid size={{ xs: 6, md: 6, lg: 3 }} textAlign="center">
          <Typography variant="subtitle2" fontWeight={600} gutterBottom>
            Credit Cards
          </Typography>
          {creditCards.map((item, index) => (
            <IconButton size="small" key={index}>
              <Box component="img" sx={{ height: 28 }} src={item} />
            </IconButton>
          ))}
        </Grid>
      </Grid>

      <Divider sx={{ p: 2 }} />

      <Box
        sx={{
          display: "flex",
          flexDirection: { xs: "column", sm: "row" },
          justifyContent: "space-between",
          alignItems: "center",
          textAlign: { xs: "center", sm: "left" },
          px: 2,
          py: 2,
        }}
      >
        <Typography
          m={1}
          variant="body2"
          color="textSecondary"
          fontWeight="bold"
        >
          © Copyright 2025 - Abbsium
        </Typography>
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            fontSize: "14px",
            fontWeight: "bold",
            mt: 0.8,
          }}
        >
          <RouterLink
            to="/privacy-policy"
            style={{ color: "black", textDecoration: "none" }}
          >
            Privacy Policy
          </RouterLink>
          <Typography
            variant="body2"
            component="span"
            color="textSecondary"
            sx={{ mx: 2 }}
          >
            |
          </Typography>
          <RouterLink
            to="/terms"
            style={{ color: "black", textDecoration: "none" }}
          >
            Terms of Use
          </RouterLink>
        </Box>
      </Box>
    </Box>
  );
};

export default Footer;
