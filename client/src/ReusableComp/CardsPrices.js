import React, { useState } from "react";
import {
  Box,
  Typography,
  Button,
  Card,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Alert,
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CancelIcon from "@mui/icons-material/Cancel";
import { BeatLoader } from "react-spinners";

import { createPayment } from "../Services/PaymentService";
import { loadStripe } from "@stripe/stripe-js";
import { useNavigate } from "react-router-dom";

const stripePromise = loadStripe(
  "pk_test_51M4otcDgpFmzFNRzBx4GqyP1lxRnF517Hn4GQvNeYmrST7tts5BWjkZOlsaAtU3ZHh3aN3YnHqUNG3BU1mxxLbHw00MZl2NK0k"
);

const CardsPrices = (props) => {
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();

  const handleCheckout = async (price, product) => {
    try {
      setLoading(true); // Activa el spinner
      const session = localStorage.getItem("TOKEN_KEY");

      if (session) {
        const response = await createPayment(price, product);
        const stripe = await stripePromise;
        await stripe.redirectToCheckout({
          sessionId: response.sessionId,
        });
      } else {
        navigate("/login");
      }
    } catch (error) {
      console.error("Error during checkout:", error);
    } finally {
      setLoading(false); // Desactiva el spinner si hay error
    }
  };

  return (
    <Card
      sx={{
        p: 3,
        borderRadius: 3,
        textAlign: "center",
        boxShadow: "7px 0px 0px 0px #0399DF",
        position: "relative",
        overflow: "hidden",
        backgroundColor: "#fff",
        border: "2px solid #0399DF",
      }}
    >
      {/* Diagonal Ribbon */}
      <Box
        sx={{
          position: "absolute",
          top: 14,
          left: -50,
          width: 200,
          transform: "rotate(-45deg)",
          backgroundColor: "#0399DF",
          color: "white",
          fontSize: "0.75rem",
          fontWeight: "bold",
          py: 0.5,
          px: 0,
          textAlign: "center",
          zIndex: 2,
          lineHeight: 1.8,
        }}
      >
        <Typography mr={3}>{props.offer}</Typography>
      </Box>

      <Typography variant="h6" fontWeight="bold" mt={4}>
        Unlock Exclusive Content
      </Typography>

      <Typography
        variant="h5"
        sx={{
          mt: 1,
          fontWeight: "bold",
          color: "#0399DF",
          fontSize: "1.5rem",
        }}
      >
        {props.price}/
        <Typography
          component="span"
          fontSize="1rem"
          color="#0399DF"
          fontWeight="bold"
        >
          {props.subscription}
        </Typography>
      </Typography>

      <Box sx={{ p: 2 }}>
        <List>
          {props.packageContainer.map((text, index) => (
            <ListItem sx={{ m: -2 }} key={index}>
              <ListItemIcon>
                {text.type ? (
                  <CheckCircleIcon sx={{ color: "green" }} />
                ) : (
                  <CancelIcon sx={{ color: "red" }} />
                )}
              </ListItemIcon>
              <ListItemText primary={text.label} />
            </ListItem>
          ))}
        </List>
      </Box>

      {props.offer !== "Basic !!!" && (
        <Alert sx={{ borderRadius: 3 }} severity="info">
          <Typography variant="body2" fontWeight="bold" color="text.primary">
            Customer support 24h
          </Typography>
        </Alert>
      )}

      <Box
        sx={{
          display: "flex",
          justifyContent: "center",
          alignItems: "center",
          width: "100%", // Ensure the box takes full width for centering
          mt: 2,
        }}
      >
        <Button
          variant="contained" // Material-UI contained button style
          disabled={loading} // Disable the button when loading to prevent multiple clicks
          onClick={() => handleCheckout(props.price, "Website")} // Call the checkout handler
          sx={{
            backgroundColor: "#0399DF", // Custom background color (blue)
            borderRadius: "30px", // Rounded corners for a pill shape
            minWidth: "100%", // Ensures the button takes full width of its container (Box)
            height: "40px", // Fixed height for a compact look
            textTransform: "none", // Prevent uppercase text
            fontWeight: "bold", // Bold text
            fontSize: "0.95rem", // Slightly smaller font size
            border: "2px solid #0399DF", // Blue border
            boxShadow: "none", // Remove default shadow
            display: "flex", // Use flexbox for internal centering of content
            justifyContent: "center", // Center content horizontally
            alignItems: "center", // Center content vertically
            // Hover effects for a more interactive feel
            ":hover": {
              backgroundColor: "white", // White background on hover
              color: "#0399DF", // Blue text on hover
              fontWeight: "bold", // Keep text bold on hover
              boxShadow: "none", // No shadow on hover
            },
            // Styles for when the button is disabled
            "&.Mui-disabled": {
              backgroundColor: "white", // Lighter blue when disabled
              color: "white", // White text when disabled
              border: "2px solid #0399DF", // Lighter blue border when disabled
            },
          }}
        >
          {loading ? (
            <BeatLoader
              color="#0399DF"
              size={18}
              aria-label="Loading Spinner"
              data-testid="loader"
            />
          ) : (
            "Choose Plan"
          )}
        </Button>
      </Box>
    </Card>
  );
};

export default CardsPrices;
