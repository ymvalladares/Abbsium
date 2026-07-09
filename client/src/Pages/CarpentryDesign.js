import { Box, Chip, Typography } from "@mui/material";
import FlipCard from "../ReusableComp/FlipCard";
import { useEffect, useRef, useState } from "react";
import Bedroom1 from "../Pictures/bedroom1.jpeg";
import Bedroom2 from "../Pictures/bedroom2.jpeg";
import Bedroom1Back from "../Pictures/bedroomSize1.png";
import Bedroom2Back from "../Pictures/bedroomSize2.png";
import Footer from "../Components/Footer";

const cardData = [
  {
    id: 0,
    frontImage: Bedroom1,
    backImage: Bedroom1Back,
    price: 199,
    title: "Bedroom",
    description:
      "This plush sofa looks like the perfect place to curl up with a good book.",
  },
  {
    id: 1,
    frontImage: Bedroom2,
    backImage: Bedroom2Back,
    price: 220,
    title: "Bedroom",
    description:
      "This plush sofa looks like the perfect place to curl up with a good book.",
  },
  {
    id: 2,
    frontImage: Bedroom1,
    backImage: Bedroom1Back,
    price: 275,
    title: "Bedroom",
    description:
      "This plush sofa looks like the perfect place to curl up with a good book.",
  },
  {
    id: 3,
    frontImage: Bedroom1,
    backImage: Bedroom1Back,
    price: 165,
    title: "Bedroom",
    description:
      "This plush sofa looks like the perfect place to curl up with a good book.",
  },
];
const CarpentryDesign = () => {
  const [flippedIndex, setFlippedIndex] = useState(null);
  const [unlockedCards, setUnlockedCards] = useState([]);
  const containerRef = useRef(null);

  const handleToggle = (index) => {
    //Request to open Stripe from server

    setFlippedIndex((prev) => (prev === index ? null : index));
    if (!unlockedCards.includes(index)) {
      setUnlockedCards((prev) => [...prev, index]);
    }
  };

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (
        containerRef.current &&
        !containerRef.current.contains(event.target)
      ) {
        setFlippedIndex(null);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  return (
    <>
      <Box
        sx={{
          maxWidth: "900px",
          mx: "auto",
          px: { xs: 2, sm: 4 },
          py: { xs: 4, sm: 6 },
          textAlign: "center",
        }}
      >
        <Chip
          label="Professional Designs"
          color="primary"
          variant="outlined"
          sx={{
            backgroundColor: "#E3F0FE",
            border: "none",
            borderRadius: "10px",
            fontWeight: "bold",
            fontSize: "0.9rem",
            px: 2,
            py: 1,
            mb: 1,
          }}
        />

        <Typography
          variant="h4"
          fontWeight={700}
          sx={{ mb: 1, fontSize: { xs: "1.8rem", sm: "2.2rem" } }}
        >
          Custom Woodwork That Elevates Your Home
        </Typography>

        <Typography
          variant="body1"
          color="text.secondary"
          sx={{
            mb: 6,
            maxWidth: "700px",
            mx: "auto",
            fontSize: { xs: "1rem", sm: "1.1rem" },
          }}
        >
          Explore tailored carpentry crafted with precision and elegance. Our
          custom woodwork blends functionality with timeless design—from
          statement-making bedroom sets to built-in solutions that fit your
          lifestyle. Transform your space with expert craftsmanship.
        </Typography>
      </Box>

      <Box
        ref={containerRef}
        sx={{
          display: "flex",
          flexWrap: "wrap",
          gap: 4,
          justifyContent: "center",
          mt: -5,
        }}
      >
        {cardData.map((card, index) => (
          <FlipCard
            key={card.id}
            isFlipped={flippedIndex === index}
            onToggle={() => handleToggle(index)}
            frontImage={card.frontImage}
            backImage={card.backImage}
            price={card.price}
            title={card.title}
            description={card.description}
            unlocked={unlockedCards.includes(index)}
          />
        ))}
      </Box>
      <Footer />
    </>
  );
};

export default CarpentryDesign;
