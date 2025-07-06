import React from "react";
import {
  Card,
  CardContent,
  Box,
  Typography,
  Button,
  Divider,
  Alert,
} from "@mui/material";
import { styled } from "@mui/system";
import LockPersonIcon from "@mui/icons-material/LockPerson";
import EmojiEventsIcon from "@mui/icons-material/EmojiEvents";
import CloudDownloadIcon from "@mui/icons-material/CloudDownload";
import { Download } from "@mui/icons-material";

const FlipCardContainer = styled(Box)(({ theme }) => ({
  perspective: "1000px",
  width: "360px",
  height: "430px",
  padding: theme.spacing(3),
  [theme.breakpoints.down("sm")]: {
    width: "90%",
    height: "430px",
    padding: theme.spacing(2),
  },
}));

const FlipCardInner = styled(Box, {
  shouldForwardProp: (prop) => prop !== "flipped",
})(({ flipped }) => ({
  position: "relative",
  width: "100%",
  height: "100%",
  transformStyle: "preserve-3d",
  transition: "transform 0.6s",
  transform: flipped ? "rotateY(180deg)" : "rotateY(0deg)",
  borderRadius: "15px",
}));

const FlipCardFace = styled(Card, {
  shouldForwardProp: (prop) => prop !== "front",
})(({ front }) => ({
  position: "absolute",
  width: "100%",
  height: "100%",
  backfaceVisibility: "hidden",
  display: "flex",
  flexDirection: "column",
  justifyContent: "flex-start",
  borderRadius: "15px",
  border: "1px solid #EDF4FB",
  ...(front ? {} : { transform: "rotateY(180deg)" }),
}));

const Image = styled("img")({
  width: "100%",
  height: "180px",
  objectFit: "cover",
  borderRadius: "25px",
  padding: 10, // ✅ padding horizontal
  marginTop: 8,
  marginBottom: 12,
});

export default function FlipCard({
  isFlipped,
  onToggle,
  frontImage,
  backImage,
  price,
  unlocked,
  title,
  description,
}) {
  return (
    <FlipCardContainer>
      <FlipCardInner flipped={isFlipped}>
        {/* FRONT FACE */}
        <FlipCardFace front>
          <CardContent sx={{ p: 0 }}>
            <Image src={frontImage} alt="Front" />
            <Divider />
            <Box
              sx={{
                px: 2,
                py: 2,
                fontFamily: "Arial, sans-serif",
              }}
            >
              <Box
                display="flex"
                justifyContent="space-between"
                alignItems="center"
                mb={1}
              >
                <Typography variant="h6" fontWeight="bold">
                  Sofa
                </Typography>
                <Box display="flex" gap={1}>
                  <Typography
                    variant="body2"
                    sx={{ textDecoration: "line-through", color: "#888" }}
                  >
                    ${price + 180}
                  </Typography>
                  <Typography variant="h6" fontWeight="bold">
                    ${price}
                  </Typography>
                </Box>
              </Box>

              {/* Descripción */}
              <Typography variant="body2" color="text.secondary" mb={2}>
                {description}
              </Typography>

              <Button
                fullWidth
                startIcon={unlocked ? <EmojiEventsIcon /> : <LockPersonIcon />}
                variant="contained"
                size="small"
                onClick={onToggle}
                sx={{
                  backgroundColor: "#EDF4FB",
                  color: "#0399DF",
                  borderRadius: 2,
                  textTransform: "none",
                  fontWeight: "bold",
                  fontSize: "0.8rem",
                  border: "2px solid #0399DF",
                  boxShadow: "0px 2px 5px rgba(0,0,0,0.1)",
                  ":hover": {
                    backgroundColor: "white",
                    boxShadow: "0px 2px 5px rgba(0,0,0,0.15)",
                  },
                }}
              >
                {unlocked ? "View Plans" : "Unlock to see plans"}
              </Button>
            </Box>
          </CardContent>
        </FlipCardFace>

        <FlipCardFace>
          <CardContent sx={{ textAlign: "center" }}>
            <Image src={backImage} alt="Back" />
            <Divider />
            <Box sx={{ alignItems: "center", mt: 1 }}>
              <Alert sx={{ borderRadius: 3 }} severity="info">
                Thanks for your purchase
              </Alert>
              <Button
                fullWidth
                startIcon={<CloudDownloadIcon />}
                variant="contained"
                size="small"
                sx={{
                  backgroundColor: "#EDF4FB",
                  color: "#0399DF",
                  borderRadius: 2,
                  textTransform: "none",
                  fontWeight: "bold",
                  fontSize: "0.8rem",
                  border: "2px solid #0399DF",
                  mt: 5,
                  boxShadow: "0px 2px 5px rgba(0,0,0,0.1)",
                  ":hover": {
                    backgroundColor: "white",
                    boxShadow: "0px 2px 5px rgba(0,0,0,0.15)",
                  },
                }}
              >
                Download Plans
              </Button>
            </Box>
          </CardContent>
        </FlipCardFace>
      </FlipCardInner>
    </FlipCardContainer>
  );
}
