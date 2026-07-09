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
import ClearIcon from "@mui/icons-material/Clear";
import payment_denied from "../Pictures/payment_denied.jpg";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useEffect } from "react";
import { Pots_Request } from "../Services/PaymentService";

const CustomConnector = styled(StepConnector)(({ theme }) => ({
  [`&.MuiStepConnector-alternativeLabel`]: {
    top: 14,
    marginRight: -10,
  },
  [`& .MuiStepConnector-line`]: {
    height: 7,
    border: 0,
    backgroundColor: "transparent", // por defecto sin color
  },
  [`&.Mui-active .MuiStepConnector-line, &.Mui-completed .MuiStepConnector-line`]:
    {
      backgroundColor: "#FD3456",
    },
}));

// Ícono personalizado
function CustomStepIcon(props) {
  const { active, completed, icon, className } = props;
  const isLastStep = icon === 3;

  return (
    <Box
      className={className}
      sx={{
        backgroundColor: active || completed ? "#FD3456" : "grey.300",
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
      {!isLastStep && (active || completed) ? (
        <ClearIcon fontSize="small" />
      ) : (
        icon
      )}
    </Box>
  );
}

const steps = ["Sites selected", "Payment received", "Order Ready to Ship"];
const Failure_payment = () => {
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
              src={payment_denied}
              alt="Payment denied"
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
              color="#FD3456"
              variant="h5"
              fontWeight="bold"
              gutterBottom
            >
              Payment Denied
            </Typography>
            <Typography variant="body1" color="text.secondary" mb={3}>
              Thank you for choosing Abbsium. Unfortunately, your Payment could
              not be accepted. Please check your payment method and try again.
              If you need assistance, do not hesitate to contact us.
            </Typography>

            <Box mb={3} sx={{ width: "100%", px: isMobile ? 0 : 1 }}>
              <Stepper
                activeStep={1}
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
                  backgroundColor: "#FD3456",
                  color: "white",
                  fontWeight: "bold",
                  borderRadius: "10px",
                  boxShadow: "0 4px 24px 0 #0004",
                  ":hover": {
                    color: "#FD3456",
                    backgroundColor: "white",
                    textDecoration: "underline",
                    border: "1px solid #FD3456",
                  },
                }}
              >
                Go back to Dashboard
              </Button>
              <Button
                color="primary"
                onClick={() => navigate("/prices")}
                sx={{
                  px: 3,
                  textTransform: "none",
                  border: "1px solid #FD3456",
                  backgroundColor: "white",
                  color: "#FD3456",
                  fontWeight: "bold",
                  borderRadius: "10px",
                  boxShadow: "0 4px 24px 0 #0002",
                  ":hover": {
                    color: "white",
                    backgroundColor: "#FD3456",
                    textDecoration: "underline",
                    border: "1px solid #FD3456",
                  },
                }}
              >
                Go to Plans
              </Button>
            </Box>
          </Grid>
        </Grid>
      </Container>
    </Box>
  );
};

export default Failure_payment;
