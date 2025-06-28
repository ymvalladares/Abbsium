import React from "react";
import Header from "../Components/Header";
import Footer from "../Components/Footer";
import { Box, Card, Typography, Grid } from "@mui/material";
import HomeIcon from "@mui/icons-material/Home";
import HouseIcon from "@mui/icons-material/House";
import ApartmentIcon from "@mui/icons-material/Apartment";
import WorkIcon from "@mui/icons-material/Work";
import VillaIcon from "@mui/icons-material/Villa";

const propertyTypes = [
  {
    title: "SEO Optimization",
    description: "Improve your online visibility with targeted strategies.",
    icon: <HomeIcon sx={{ fontSize: 32, color: "#749AFF" }} />,
    bgcolor: "#D8F6FF",
  },
  {
    title: "Fast & Reliable",
    description: "We work with clear timelines and transparent results.",
    icon: <HouseIcon sx={{ fontSize: 32, color: "#749AFF" }} />,
    bgcolor: "#D8F6FF",
  },
  {
    title: "Ongoing Support",
    description: "We provide continued assistance support 24h.",
    icon: <ApartmentIcon sx={{ fontSize: 32, color: "#749AFF" }} />,
    bgcolor: "#D8F6FF",
  },
  {
    title: "Custom Design",
    description: "Tailored web and marketing design for your business.",
    icon: <WorkIcon sx={{ fontSize: 32, color: "#749AFF" }} />,
    bgcolor: "#D8F6FF",
  },
  {
    title: "Security & Reliability",
    description: "We prioritize secure platforms that protect your data",
    icon: <VillaIcon sx={{ fontSize: 32, color: "#749AFF" }} />,
    bgcolor: "#D8F6FF",
  },
];

const Dashboard = () => {
  return (
    <>
      <Header />
      <Box
        sx={{ textAlign: "center", py: 6, px: 2, maxWidth: 1200, mx: "auto" }}
      >
        <Typography color="#709CFF" variant="h4" fontWeight="bold" mb={1}>
          Why Choose Us
        </Typography>
        <Typography variant="subtitle1" color="text.secondary" mb={5}>
          At Abbsium, we combine technology, strategy, and creativity to help
          you stand out in the digital world. Whether you need a high-converting
          website, personalized marketing strategies, or continuous technical
          support, our expert team is committed to delivering exceptional
          results tailored to your business goals.
        </Typography>

        <Grid mb={4} container spacing={3} justifyContent="center">
          {propertyTypes.map((item, index) => (
            <Grid
              size={{ xs: 6, sm: 4, md: 2.4 }}
              key={index}
              letterSpacing={2.4}
            >
              <Card
                sx={{
                  py: 4,
                  px: 2,
                  textAlign: "center",
                  borderRadius: 3,
                  cursor: "pointer",
                  boxShadow: "none",
                  border: `2px solid ${item.bgcolor}`,
                  height: "100%",
                  display: "flex",
                  flexDirection: "column",
                  justifyContent: "center",
                }}
              >
                <Box
                  mb={2}
                  sx={{
                    width: 47,
                    height: 47,
                    mx: "auto",
                    borderRadius: "25%",
                    backgroundColor: item.bgcolor,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                  }}
                >
                  {item.icon}
                </Box>
                <Typography variant="subtitle1" fontWeight={600} mb={1}>
                  {item.title}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {item.description}
                </Typography>
              </Card>
            </Grid>
          ))}
        </Grid>
      </Box>
      <Footer />
    </>
  );
};

export default Dashboard;
