import { Box, Chip, Container, Divider, Grid, Typography } from "@mui/material";
import InvestmentCard from "../ReusableComp/InvestmentCard";
import Eureka_Picture from "../Pictures/Eureka.png";
import Pawns from "../Pictures/PawnsApp.png";
import ySense from "../Pictures/ySense.avif";
import HoneyGain from "../Pictures/HoneyGain.jpg";
import Robinhood from "../Pictures/robinhoo.png";
import Rakuten from "../Pictures/Rakuten.jpeg";
import Footer from "../Components/Footer";

const items = [
  {
    name: "Eureka",
    bg: Eureka_Picture,
    link: "https://join.robinhood.com/yordanm-257ffe",
  },
  {
    name: "Pawns.App",
    bg: Pawns,
    link: "https://discoverpawns.eu/15098663",
  },
  {
    name: "ySense",
    bg: ySense,
    link: "https://www.ysense.com/?rb=215466772",
  },
  {
    name: "Rakuten",
    bg: Rakuten,
    link: "https://www.rakuten.com/r/YORDAN477?eeid=28187",
  },
  {
    name: "HoneyGain",
    bg: HoneyGain,
    link: "https://r.honeygain.me/YORDAD78D1",
  },
  {
    name: "Robinhood",
    bg: Robinhood,
    link: "https://join.robinhood.com/yordanm-257ffe",
  },
];

const Games = () => {
  return (
    <>
      <Box
        sx={{
          textAlign: "center",
          my: 4,
          px: 2,
        }}
      >
        <Chip
          label="Smart Investments"
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
          component="h2"
          gutterBottom
          sx={{ fontWeight: "bold" }}
        >
          Discover Profitable Ways to Grow Your Money
        </Typography>
        <Typography variant="body1" color="text.secondary">
          Explore games that pay, stock trading platforms, and other modern
          investment strategies – all in one place.
        </Typography>
      </Box>

      <Container>
        <Box sx={{ position: "relative", mt: 3, mb: 5 }}>
          <Divider sx={{ ml: 1, mr: 1 }} />
          <Typography
            variant="caption"
            sx={{
              position: "absolute",
              top: "-10px",
              left: "35px",
              backgroundColor: "white",
              px: 1,
              color: "text.secondary",
              fontWeight: "bold",
              fontSize: "0.75rem",
            }}
          >
            Make money playing
          </Typography>
        </Box>
        <Grid mb={10} container spacing={6} justifyContent="center">
          {items.map((item, index) => (
            <Grid item xs={6} sm={4} md={2} key={index}>
              <InvestmentCard bg={item.bg} name={item.name} link={item.link} />
            </Grid>
          ))}
        </Grid>
      </Container>
      <Footer />
    </>
  );
};

export default Games;
