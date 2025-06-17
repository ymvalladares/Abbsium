import React from "react";
import { Box, Grid, Typography, IconButton, Divider } from "@mui/material";
import AbbsiumLogo from "../Pictures/abbsium192.png";
import { Link, Link as RouterLink } from "react-router-dom";

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
      sx={{
        background: "#fff",
        background:
          "linear-gradient(165deg,rgba(255, 255, 255, 1) 56%, rgba(216, 246, 255, 1) 80%)",

        pt: 4,
        px: { xs: 3, md: 10 },
        borderTop: "1px solid #E0E0E0",
        mt: 8,
      }}
    >
      {/* CTA Section */}
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

      {/* Footer Links */}
      <Grid container spacing={4} justifyContent="space-between">
        <Grid
          item
          xs={12}
          md={3}
          sx={{
            display: "flex",
            justifyContent: "center",
            alignItems: "center",
          }}
        >
          <Box component="img" src={AbbsiumLogo} height="64px" />
        </Grid>

        <Grid item xs={6} md={2}>
          <Typography variant="subtitle2" fontWeight={600} gutterBottom>
            Contact
          </Typography>
          <Typography variant="body2">+1 (904) 852 3178</Typography>
        </Grid>

        <Grid item xs={6} md={2}>
          <Typography variant="subtitle2" fontWeight={600} gutterBottom>
            Support
          </Typography>
          <Typography variant="body2">Support Request</Typography>
          <Typography variant="body2">Contact</Typography>
        </Grid>

        <Grid item xs={6} md={2}>
          <Typography
            textAlign="center"
            variant="subtitle2"
            fontWeight={600}
            gutterBottom
          >
            Follow Me
          </Typography>
          <Box>
            {socialMedia.map((item, index) => (
              <IconButton size="small" key={index}>
                <Box component="img" sx={{ height: 28 }} src={item} />
              </IconButton>
            ))}
          </Box>
        </Grid>

        <Grid item xs={6} md={2}>
          <Typography
            textAlign="center"
            variant="subtitle2"
            fontWeight={600}
            gutterBottom
          >
            Credit Cards
          </Typography>
          <Box>
            {creditCards.map((item, index) => (
              <IconButton size="small" key={index}>
                <Box component="img" sx={{ height: 28 }} src={item} />
              </IconButton>
            ))}
          </Box>
        </Grid>
      </Grid>

      <Divider sx={{ p: 2 }} />

      <Box
        sx={{
          display: { xs: "block", sm: "flex" },
          justifyContent: "space-between",
          p: 2,
          ml: { xs: 10, sm: 0 },
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
          <Link
            component={RouterLink}
            to="/privacy-policy"
            sx={{ color: "black", textDecoration: "none" }}
          >
            Privacy Policy
          </Link>
          <Typography
            variant="body2"
            component="span"
            color="textSecondary"
            sx={{ mx: 2 }}
          >
            |
          </Typography>
          <Link
            component={RouterLink}
            to="/terms"
            sx={{ color: "black", textDecoration: "none" }}
          >
            Terms of Use
          </Link>
        </Box>
      </Box>
    </Box>
  );
};

export default Footer;
