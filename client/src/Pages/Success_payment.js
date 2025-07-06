import React from "react";
import {
  Box,
  Typography,
  Button,
  Container,
  Step,
  StepLabel,
  Stepper,
  StepConnector,
  Grid,
  useMediaQuery,
} from "@mui/material";
import { useTheme, styled } from "@mui/material/styles";
import Check from "@mui/icons-material/Check";
import success_img from "../Pictures/success_payment.avif";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useEffect } from "react";
import { Pots_Request } from "../Services/PaymentService";

// Conector personalizado
const CustomConnector = styled(StepConnector)(({ theme }) => ({
  [`&.MuiStepConnector-alternativeLabel`]: {
    top: 14,
  },
  [`&.MuiStepConnector-root`]: {
    marginRight: -10,
  },
  [`& .MuiStepConnector-line`]: {
    height: 7,
    border: 0,
    backgroundColor: "#AFCFE4",
  },
}));

// Ícono personalizado
function CustomStepIcon(props) {
  const { active, completed, icon, className } = props;

  return (
    <Box
      className={className}
      sx={{
        backgroundColor: active || completed ? "#5866FB" : "grey.300",
        zIndex: 1,
        color: "#fff",
        width: 32,
        height: 32,
        display: "flex",
        borderRadius: "50%",
        justifyContent: "center",
        alignItems: "center",
      }}
    >
      {completed ? <Check fontSize="small" /> : icon}
    </Box>
  );
}

const steps = ["Sites selected", "Payment received", "Order Ready to Ship"];

const Success_payment = () => {
  const navigate = useNavigate();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const [searchParams] = useSearchParams();
  const sessionId = searchParams.get("session_id");

  useEffect(() => {
    if (sessionId) {
      Pots_Request(`${window.BaseUrlGeneral}Order/verify`, {
        sessionId,
      })
        .then(() => {
          console.log("Order save success");
        })
        .catch((error) => {
          console.log("Payment not confirmed", error);
        });
    }
  }, []);

  return (
    <Box
      height="100%"
      width="100%"
      display="flex"
      alignItems="center"
      justifyContent="center"
      padding={2}
    >
      <Container maxWidth="lg">
        <Grid container spacing={4} alignItems="center" justifyContent="center">
          {/* Right side: Image */}
          <Grid size={{ xs: 12, md: 5 }} display="flex" justifyContent="center">
            <Box
              component="img"
              src={success_img}
              alt="Success illustration"
              sx={{
                maxWidth: "100%",
                width: isMobile ? "80%" : "100%",
                maxHeight: isMobile ? 300 : 450,
                height: "auto",
              }}
            />
          </Grid>

          {/* Left side: Centered content */}
          <Grid
            size={{ xs: 12, md: 7 }}
            display="flex"
            flexDirection="column"
            alignItems="center"
            textAlign="center"
          >
            <Typography
              color="#5866FB"
              variant="h5"
              fontWeight="bold"
              gutterBottom
            >
              Payment successful
            </Typography>
            <Typography variant="body1" color="text.secondary" mb={3}>
              Thank you for choosing Abbsium. Your order will be generated soon.
              Any questions, please do not hesitate to contact us.
            </Typography>

            <Box mb={3} sx={{ width: "100%", px: isMobile ? 0 : 1 }}>
              <Stepper
                activeStep={2}
                alternativeLabel
                connector={<CustomConnector />}
                sx={{ width: "100%" }}
              >
                {steps.map((label) => (
                  <Step key={label}>
                    <StepLabel StepIconComponent={CustomStepIcon}>
                      <Box sx={{ fontWeight: 500 }}>{label}</Box>
                    </StepLabel>
                  </Step>
                ))}
              </Stepper>
            </Box>

            <Box
              sx={{
                mt: 2,
                display: "flex",
                flexWrap: "wrap",
                justifyContent: "center",
                gap: 2,
              }}
            >
              <Button
                color="primary"
                onClick={() => navigate("/dashboard")}
                sx={{
                  px: 3,
                  textTransform: "none",
                  backgroundColor: "#5866FB",
                  color: "white",
                  fontWeight: "bold",
                  borderRadius: "10px",
                  boxShadow: "0 4px 24px 0 #0004",
                  ":hover": {
                    color: "#5B76CD",
                    backgroundColor: "white",
                    textDecoration: "underline",
                    border: "1px solid #5866FB",
                  },
                }}
              >
                Go back to Dashboard
              </Button>
              <Button
                color="primary"
                onClick={() => navigate("/orders")}
                sx={{
                  px: 3,
                  textTransform: "none",
                  border: "1px solid #5866FB",
                  backgroundColor: "white",
                  color: "#5B76CD",
                  fontWeight: "bold",
                  borderRadius: "10px",
                  boxShadow: "0 4px 24px 0 #0002",
                  ":hover": {
                    color: "white",
                    backgroundColor: "#5866FB",
                    textDecoration: "underline",
                    border: "1px solid #5866FB",
                  },
                }}
              >
                Go to Orders
              </Button>
            </Box>
          </Grid>
        </Grid>
      </Container>
    </Box>
  );
};

export default Success_payment;
