import React from "react";
import { Box, Grid, Typography, Paper, Button, Chip } from "@mui/material";
import { styled } from "@mui/material/styles";
import BusinessIcon from "@mui/icons-material/Business"; // Sample icons
import AssessmentIcon from "@mui/icons-material/Assessment";
import DescriptionIcon from "@mui/icons-material/Description";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";
import BarChartIcon from "@mui/icons-material/BarChart";
import TrendingUpIcon from "@mui/icons-material/TrendingUp";
import SecurityIcon from "@mui/icons-material/Security";
import EmojiObjectsIcon from "@mui/icons-material/EmojiObjects";
import { Link } from "react-router-dom";
import Footer from "../Components/Footer";

const features = [
  {
    title: "Web Site Design",
    description:
      "Modern, responsive websites tailored for your brand’s identity.",
    icon: <AssessmentIcon fontSize="large" />,
  },
  {
    title: "CEO Optimization",
    description: "Boost visibility and rank higher on Google search results.",
    icon: <DescriptionIcon fontSize="large" />,
    highlighted: true,
  },
  {
    title: "Email Service",
    description: "Professional email campaigns to reach and engage clients.",
    icon: <BusinessIcon fontSize="large" />,
  },
  {
    title: "Secure Payment",
    description:
      "Fast, encrypted transactions for complete customer confidence.",
    icon: <VerifiedUserIcon fontSize="large" />,
  },

  {
    title: "Smart Chatbot",
    description: "AI-powered assistant available 24/7 to support visitors.",
    icon: <BarChartIcon fontSize="large" />,
  },
  {
    title: "Analitic Market",
    description: "Track behavior, trends, and data-driven performance metrics.",
    icon: <TrendingUpIcon fontSize="large" />,
  },
  {
    title: "Affiliate Marketing",
    description:
      "Expand your reach through performance-based partner promotions.",
    icon: <SecurityIcon fontSize="large" />,
    highlighted: true,
  },
  {
    title: "Investment Advice",
    description:
      "Strategic insights to grow and manage your digital investment.",
    icon: <EmojiObjectsIcon fontSize="large" />,
  },
];

const FeatureCard = styled(Paper)(({ theme, highlighted }) => ({
  padding: theme.spacing(4),
  textAlign: "center",
  borderRadius: 16,
  backgroundColor: highlighted ? theme.palette.primary.main : "#fff",
  color: highlighted ? "#fff" : "#000",
  transition: "0.3s",

  boxShadow: "none",
  border: "2px solid #E3F0FE",
  "&:hover": {
    boxShadow: theme.shadows[6],
  },
}));

export default function Services() {
  return (
    <>
      <Box
        sx={{
          py: { xs: 6, md: 10 },
          px: { xs: 2, md: 6 },
          textAlign: "center",
          mt: { xs: 1, md: -4 },
        }}
      >
        <Chip
          sx={{
            backgroundColor: "#E3F0FE",
            border: "none",
            borderRadius: "10px",
            fontWeight: "bold",
            fontSize: "0.9rem",
            padding: "15px",
          }}
          label="What we do for you"
          color="primary"
          variant="outlined"
        />

        <Typography variant="h4" fontWeight={700} sx={{ mb: 2 }}>
          Empowering Your Business Through Smart Digital Solutions
        </Typography>

        <Typography variant="body1" sx={{ mb: 6, maxWidth: 700, mx: "auto" }}>
          At Abbsium, we provide a full spectrum of digital and strategic
          services to help your business grow faster and operate smarter. From
          financial planning to market analysis, each service is tailored to
          drive measurable success and lasting impact.
        </Typography>
        <Grid container spacing={4} justifyContent="center">
          {features.map((feature, index) => (
            <Grid size={{ xs: 12, sm: 6, md: 3 }} key={index}>
              <FeatureCard highlighted={feature.highlighted ? 1 : 0}>
                <Box
                  mb={2}
                  sx={{
                    width: 64,
                    height: 64,
                    mx: "auto",
                    borderRadius: "25%",
                    backgroundColor: feature.highlighted ? "#fff" : "#E3F0FE",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                  }}
                >
                  {React.cloneElement(feature.icon, {
                    style: {
                      color: "#1976D2",
                      fontSize: 30,
                    },
                  })}
                </Box>
                <Typography variant="h6" fontWeight={600} gutterBottom>
                  {feature.title}
                </Typography>
                <Typography variant="body2" sx={{ mb: 2 }}>
                  {feature.description}
                </Typography>
                <Link to="/contacts">
                  <Button
                    variant="text"
                    sx={{
                      color: feature.highlighted ? "#fff" : "primary.main",
                      textTransform: "none",
                    }}
                  >
                    See Details →
                  </Button>
                </Link>
              </FeatureCard>
            </Grid>
          ))}
        </Grid>
      </Box>
      <Footer />
    </>
  );
}
