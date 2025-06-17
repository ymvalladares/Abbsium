import React from "react";

import { Box, Grid, Stack, Typography } from "@mui/material";
import CardsPrices from "../ReusableComp/CardsPrices";
import Footer from "../Components/Footer";

const packageContainer = [
  [
    "Business Consultation",
    "Professional Design",
    "Responsive",
    "Free Icon Design",
    "Limited Updates",
  ],
  [
    "Special Package (Add)",
    "Host and Domain",
    "CEO Optimization",
    "Internet Deployment",
    "Bussiness Email",
    "Smart ChatBots",
    "Unlimited Updates",
  ],
];

const offer = ["Special Offer!", "Premiun !!!"];
const price = ["149.99", "46.99"];
const subscription = ["On Time", "Month"];

const Prices = () => {
  return (
    <>
      <Box
        mt={{ xs: -3, md: -7 }}
        mb={2}
        sx={{
          textAlign: "center",
          py: { xs: 6, md: 10 },
          background: "#FFF",
          background:
            "linear-gradient(350deg,rgba(255, 255, 255, 1) 56%, rgba(216, 246, 255, 1) 80%)",
        }}
      >
        {/* Text Content */}
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
            sx={{
              color: "#0399DF",
              letterSpacing: 1,
            }}
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

      <Box sx={{ mb: 5 }}>
        <Grid
          container
          spacing={{ xs: 10, md: 20 }}
          justifyContent="center"
          alignItems="stretch"
        >
          {[0, 1].map((item, index) => (
            <Grid sx={{ display: "flex" }} key={index}>
              <Box sx={{ width: "100%" }}>
                <CardsPrices
                  offer={offer[item]}
                  price={price[item]}
                  packageContainer={packageContainer[item]}
                  subscription={subscription[item]}
                />
              </Box>
            </Grid>
          ))}
        </Grid>
      </Box>
      <Footer />
    </>
  );
};

export default Prices;
