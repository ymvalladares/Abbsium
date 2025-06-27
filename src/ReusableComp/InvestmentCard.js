import React from "react";
import {
  Card,
  CardMedia,
  CardContent,
  Typography,
  Button,
  Box,
} from "@mui/material";
import { useNavigate } from "react-router-dom";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";

const InvestmentCard = ({ bg, name, link }) => {
  const navigate = useNavigate();

  return (
    <Card
      sx={{
        width: 130,
        height: 130,
        position: "relative",
        overflow: "hidden",
        borderRadius: "16px",
        boxShadow: "none",
        border: "2px solid #E3F0FE",
        cursor: "pointer",
        "&:hover .overlay": {
          opacity: 1,
        },
      }}
    >
      <CardMedia
        component="img"
        image={bg}
        alt="Investment"
        sx={{
          width: "100%",
          height: "100%",
          objectFit: "cover",
        }}
      />

      {/* Overlay */}
      <Box
        className="overlay"
        sx={{
          position: "absolute",
          top: 0,
          left: 0,
          width: "100%",
          height: "100%",
          bgcolor: "rgba(0, 0, 0, 0.7)",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          opacity: 0,
          transition: "opacity 0.3s ease-in-out",
        }}
      >
        <Typography
          variant="subtitle2"
          sx={{
            color: "#1976D2",
            fontWeight: "bold",
            mb: 1,
          }}
        >
          {name}
        </Typography>
        <Button
          variant="contained"
          onClick={() => (window.location.href = link)}
          sx={{
            borderRadius: "50%",
            minWidth: "56px",
            minHeight: "56px",
            backgroundColor: "#E3F0FE",
            "&:hover": {
              backgroundColor: "#d0e8fd",
            },
          }}
        >
          <PlayArrowIcon sx={{ fontSize: "2.2rem", color: "#1976D2" }} />
        </Button>
      </Box>
    </Card>
  );
};

export default InvestmentCard;
