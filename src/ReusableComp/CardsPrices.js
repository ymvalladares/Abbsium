import React from "react";
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

const CardsPrices = (props) => {
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

      {/* Title */}
      <Typography variant="h6" fontWeight="bold" mt={4}>
        Unlock Exclusive Content
      </Typography>

      {/* Price */}
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
                <CheckCircleIcon sx={{ color: "green" }} />
              </ListItemIcon>
              <ListItemText primary={text} />
            </ListItem>
          ))}
        </List>
      </Box>

      <Alert sx={{ borderRadius: 3 }} severity="info">
        <Typography variant="body2" fontWeight="bold" color="text.primary">
          Customer support 24h
        </Typography>
      </Alert>

      {/* Button */}
      <Button
        variant="contained"
        sx={{
          mt: 3,
          backgroundColor: "#0399DF",
          borderRadius: 4,
          px: 12,
          py: -1,
          pt: "3px",
          pb: "3px",
          textTransform: "none",
          fontWeight: "bold",
          fontSize: "1rem",
          border: "2px solid #0399DF",
          boxShadow: "none",
          ":hover": {
            backgroundColor: "white",
            color: "#0399DF",
            textTransform: "none",
            fontWeight: "bold",
            fontSize: "1rem",
            boxShadow: "none",
          },
        }}
      >
        Choose Plan
      </Button>
    </Card>
  );
};

export default CardsPrices;
