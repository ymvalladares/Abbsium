import React from "react";
import {
  Typography,
  AppBar,
  Toolbar,
  IconButton,
  Drawer,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Button,
  Box,
  Link,
  Divider,
  Container,
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import HomeIcon from "@mui/icons-material/Home";
import BuildIcon from "@mui/icons-material/Build";
import ContactPhoneIcon from "@mui/icons-material/ContactPhone";
import InfoIcon from "@mui/icons-material/Info";
import CollectionsIcon from "@mui/icons-material/Collections";
import LocalPhoneIcon from "@mui/icons-material/LocalPhone";
import logo from "../Pictures/abbsium512.png";

const navItems = [
  { name: "Home", id: "header", icon: <HomeIcon /> },
  { name: "Services", id: "services", icon: <BuildIcon /> },
  { name: "Gallery", id: "gallery", icon: <CollectionsIcon /> },
  { name: "Reviews", id: "reviews", icon: <InfoIcon /> },
  { name: "Contacts", id: "contact", icon: <ContactPhoneIcon /> },
];

const Navbar = () => {
  const [drawerOpen, setDrawerOpen] = React.useState(false);

  const toggleDrawer = (open) => () => {
    setDrawerOpen(open);
  };
  return (
    <Box sx={{ flexGrow: 1 }}>
      <Box>
        <AppBar
          //position="fixed"
          position="static"
          color="default"
          elevation={1}
          sx={{
            boxShadow: "none",
            zIndex: 10,
            backgroundColor: "white",
            border: "none",
          }}
        >
          <Toolbar
            sx={{
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              flexWrap: "wrap",
              px: { xs: 2, sm: 4 },
            }}
          >
            <Box sx={{ display: "flex", alignItems: "center" }}>
              <IconButton
                edge="start"
                color="inherit"
                onClick={toggleDrawer(true)}
                sx={{
                  display: { xs: "block", sm: "none" },
                  color: "black",
                  mr: 1,
                  mt: 1,
                }}
              >
                <MenuIcon />
              </IconButton>
              <Box
                component="img"
                src={logo} // <-- replace with your logo path
                alt="Innovus Logo"
                sx={{
                  height: 80,
                  width: 80,
                  mr: 2,
                  display: { xs: "none", sm: "block" },
                }}
              ></Box>
              <Typography
                variant="h6"
                sx={{
                  display: { xs: "none", sm: "block" },
                  color: "#5B76CD",
                  fontWeight: "bold",
                }}
              >
                Abbsium
              </Typography>
            </Box>
            <List
              sx={{
                display: "flex",
                justifyContent: "space-between",
              }}
            >
              {navItems.map((item) => (
                <ListItem
                  sx={{
                    display: {
                      xs: "none",
                      sm: "block",
                      md: "block",
                      lg: "block",
                    },
                  }}
                  key={item.id}
                  disablePadding
                >
                  <Link
                    underline="none"
                    sx={{
                      display: "block",
                      textAlign: "center",
                      cursor: "pointer",
                      py: 1,
                      px: 2,
                      color: "#5B76CD",
                      fontWeight: "bold",
                      borderRadius: 1,
                      "&:hover": {
                        textDecoration: "underline",
                      },
                    }}
                  >
                    {item.name}
                  </Link>
                </ListItem>
              ))}
            </List>
            <Button
              component="a"
              href="tel:+13054970754"
              sx={{
                // mt: { xs: 1, sm: 0 },
                color: "white",
                fontWeight: "bold",
                textTransform: "none",
                backgroundColor: "#5B76CD",
                borderRadius: "10px",
                boxShadow: "0 4px 24px 0 #0004",
                px: 2.5,
                py: 1,
                display: "flex",
                alignItems: "center",
                ":hover": {
                  color: "#5B76CD",
                  backgroundColor: "white",
                  textDecoration: "underline",
                  border: "1px solid #5B76CD",
                },
              }}
              variant="outlined"
            >
              <LocalPhoneIcon sx={{ mr: 1 }} />
              +1 305 497 0754
            </Button>
          </Toolbar>
        </AppBar>
        ;{/* Drawer */}
        <Container>
          <Drawer anchor="left" open={drawerOpen} onClose={toggleDrawer(false)}>
            <Box
              sx={{
                display: "flex",
                flexDirection: "column",
                height: "100%",
                width: 275,
                mt: "8px",
              }}
            >
              {/* Title & Divider */}
              <ListItem button>
                <ListItemIcon>
                  <Box
                    sx={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      height: "30px",
                      px: 2,
                    }}
                  >
                    <Box
                      component="img"
                      src={logo}
                      alt="Abbsium Logo"
                      sx={{
                        height: 80,
                        width: 80,
                      }}
                    />
                  </Box>
                </ListItemIcon>
                <ListItemText primary="Abbsium" />
              </ListItem>

              <Divider />
              <List sx={{ width: 250 }}>
                {navItems.map((item) => (
                  <ListItem button key={item.id}>
                    <ListItemIcon sx={{ color: "blue" }}>
                      {item.icon}
                    </ListItemIcon>
                    <ListItemText primary={item.name} />
                  </ListItem>
                ))}
              </List>

              <Box
                sx={{
                  position: "absolute",
                  bottom: 40,
                  left: "50%",
                  transform: "translateX(-50%)", // center horizontally
                  width: "80%", // or "90%" depending on desired button size
                }}
              >
                <Button sx={{ width: "100%" }} variant="outlined">
                  Sign In
                </Button>
              </Box>
            </Box>
          </Drawer>
        </Container>
      </Box>
    </Box>
  );
};

export default Navbar;
