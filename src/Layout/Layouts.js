import { Stack, Typography } from "@mui/material";
import Box from "@mui/material/Box";
import { DashboardLayout } from "@toolpad/core/DashboardLayout";
import { Outlet, useOutletContext } from "react-router-dom";
import AbbsiumLogo from "../Pictures/abbsium192.png";

function CustomAppTitle() {
  return (
    <Stack ml={2} direction="row" alignItems="center" spacing={4}>
      <Box component="img" src={AbbsiumLogo} alt="Abbsium Logo" height={30} />
      <Typography
        sx={{ display: { xs: "none", md: "block" } }}
        variant="h6"
        color="#0399DF"
        fontWeight="bold"
      >
        Abbsium
      </Typography>
    </Stack>
  );
}

function SidebarFooter({ mini }) {
  return (
    <>
      {/* <Box
        sx={{
          display: "flex",
          flexDirection: "column",
          justifyContent: "flex-end",
          alignItems: "center",
        }}
        mb={2}
      >
        <Box
          sx={{
            width: "92%", // Adjust as needed
            height: "100px", // Or a specific height
            textAlign: "center",
            borderRadius: 2,
            border: "2px solid #E3F0FE",
          }}
        >
          Bottom-Center Box
        </Box>
      </Box> */}
      <Typography
        textAlign="center"
        variant="caption"
        sx={{ m: 1, whiteSpace: "nowrap", overflow: "hidden" }}
      >
        {mini
          ? "©Abbsium"
          : `© ${new Date().getFullYear()} Made with love by Abbsium`}
      </Typography>
    </>
  );
}

function Layouts() {
  return (
    <DashboardLayout
      slots={{
        appTitle: CustomAppTitle,
        sidebarFooter: SidebarFooter,
      }}
    >
      <Outlet />
    </DashboardLayout>
  );
}

export default Layouts;
