import React from "react";
import { Box, Grid, Stack, Typography } from "@mui/material";
import CardsPrices from "../ReusableComp/CardsPrices";
import Footer from "../Components/Footer";

const plans = [
  {
    offer: "Basic !!!",
    price: "149.99",
    subscription: "On Time",
    features: [
      { label: "Professional Design", type: true },
      { label: "Responsive", type: true },
      { label: "Free Icon Design", type: true },
      { label: "Code Ownership", type: true },
      { label: "Host and Domain", type: false },
      { label: "Internet Deployment", type: false },
    ],
  },
  {
    offer: "Advance !!!",
    price: "32.99",
    subscription: "Month",
    features: [
      { label: "Basic Plan (Add)", type: true },
      { label: "Host and Domain", type: true },
      { label: "Internet Deployment", type: true },
      { label: "Unlimited Updates", type: true },
      { label: "Whatsapp Bot", type: true },
      { label: "Business Email", type: false },
      { label: "Google Business", type: false },
    ],
  },
  {
    offer: "Premium !!!",
    price: "46.99",
    subscription: "Month",
    features: [
      { label: "Advance Plan (Add)", type: true },
      { label: "Business Email", type: true },
      { label: "Google Business", type: true },
      { label: "Smart ChatBots", type: true },
    ],
  },
];

const Prices = () => {
  return (
    <Box
      sx={{
        display: "flex",
        flexDirection: "column",
        minHeight: "100vh",
      }}
    >
      <Box
        mt={{ xs: -3, md: -7 }}
        //mb={2}
        sx={{
          textAlign: "center",
          py: { xs: 6, md: 10 },
          background:
            "linear-gradient(350deg,rgba(255, 255, 255, 1) 56%, rgba(216, 246, 255, 1) 80%)",
        }}
      >
        <Stack
          sx={{
            padding: "20px 10px 0",
            maxWidth: "800px",
            mx: "auto",
            position: "relative",
            zIndex: 2,
          }}
          spacing={3}
          alignItems="center"
        >
          <Typography
            sx={{ color: "#0399DF", letterSpacing: 1 }}
            variant="h5"
            fontWeight={700}
          >
            Find the perfect plan for your business
          </Typography>

          <Typography variant="body1">
            Whether you're just getting started or scaling fast,
            <span style={{ fontWeight: "bold" }}>
              {" "}
              we have a solution tailored to your needs.
            </span>{" "}
            All plans include access to our full suite of{" "}
            <strong>web development</strong> and
            <strong> digital marketing</strong> tools — designed to help you
            build, grow, and convert more.
          </Typography>

          <Typography variant="body1" sx={{ fontWeight: 600 }}>
            Transparent pricing. No hidden fees. Cancel anytime.
          </Typography>
        </Stack>
      </Box>

      <Box sx={{ mb: 3, p: 3 }}>
        <Grid container spacing={6} justifyContent="center">
          {[0, 1, 2].map((item, index) => (
            <Grid size={{ xs: 12, md: 4 }} key={index}>
              <Box sx={{ width: "100%" }}>
                <CardsPrices
                  offer={plans[item].offer}
                  price={plans[item].price}
                  packageContainer={plans[item].features}
                  subscription={plans[item].subscription}
                />
              </Box>
            </Grid>
          ))}
        </Grid>
      </Box>
      <Footer />
    </Box>
  );
};

export default Prices;
