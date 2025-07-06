import {
  Box,
  Typography,
  Divider,
  List,
  ListItem,
  ListItemText,
  Container,
} from "@mui/material";
import GavelIcon from "@mui/icons-material/Gavel";
import PublicIcon from "@mui/icons-material/Public";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";

export default function TermsOfUse() {
  return (
    <Container maxWidth="md">
      <Box
        sx={{
          px: { xs: 2, md: 6 },
          py: { xs: 5, md: 8 },
          backgroundColor: "#fff",
          color: "#333",
          fontFamily: "Roboto, sans-serif",
        }}
      >
        {/* Header */}
        <Box display="flex" alignItems="center" gap={1} mb={2}>
          <GavelIcon color="primary" fontSize="large" />
          <Typography variant="h3" fontWeight="bold">
            Terms of Use
          </Typography>
        </Box>

        <Typography variant="body1" paragraph>
          These Terms of Use ("Terms") govern your use of the website, services,
          and digital products provided by <strong>Abbsium</strong>. By
          accessing or using our services, you agree to be bound by these Terms.
        </Typography>

        <Divider sx={{ my: 3 }} />

        {/* Sections */}
        {[
          {
            title: "1. Acceptance of Terms",
            content:
              "By using this site, you confirm that you have read, understood, and agree to be bound by these Terms and our Privacy Policy.",
          },
          {
            title: "2. Eligibility",
            content:
              "You must be at least 18 years old or of legal age in your jurisdiction to use our services.",
          },
          {
            title: "3. Use of the Website",
            items: [
              "You agree not to use the site for any unlawful or harmful purpose.",
              "You agree not to interfere with the security or functionality of the site.",
              "You are responsible for maintaining the confidentiality of your account credentials.",
            ],
          },
          {
            title: "4. Intellectual Property",
            content:
              "All content, trademarks, logos, and materials on this site are the intellectual property of Abbsium or its licensors. You may not copy, distribute, or use them without written permission.",
          },
          {
            title: "5. User Content",
            content:
              "If you submit content (e.g. reviews, comments), you grant us a non-exclusive, royalty-free, worldwide license to use, display, and reproduce that content in connection with our services.",
          },
          {
            title: "6. Payment and Subscriptions",
            content:
              "Some features may require payment or a subscription. By purchasing, you agree to the applicable pricing and billing terms provided at checkout.",
          },
          {
            title: "7. Termination",
            content:
              "We reserve the right to suspend or terminate your access to the site at our discretion, without notice, for any violation of these Terms.",
          },
          {
            title: "8. Disclaimers",
            content:
              "The website is provided 'as-is' without warranties of any kind. We do not guarantee that the service will be error-free or uninterrupted.",
          },
          {
            title: "9. Limitation of Liability",
            content:
              "Abbsium shall not be liable for any direct, indirect, incidental, or consequential damages arising out of your use of the site or services.",
          },
          {
            title: "10. Governing Law",
            content:
              "These Terms shall be governed by the laws of the State of Florida, USA, without regard to its conflict of law provisions.",
            icon: <PublicIcon fontSize="small" sx={{ mr: 1 }} />,
          },
          {
            title: "11. Modifications to Terms",
            content:
              "We may revise these Terms from time to time. The updated version will be posted here, and continued use of the site signifies acceptance.",
          },
          {
            title: "12. Contact Information",
            content: (
              <Box display="flex" alignItems="center" mb={1}>
                <VerifiedUserIcon fontSize="small" sx={{ mr: 1 }} />
                <Typography variant="body1">
                  For questions, email us at:{" "}
                  <strong>yordan.j.martinez@gmail.com</strong>
                </Typography>
              </Box>
            ),
          },
        ].map((section, index) => (
          <Box key={index} mb={4}>
            <Typography variant="h5" fontWeight="bold" gutterBottom>
              {section.title}
            </Typography>
            {section.content && (
              <Typography variant="body1" paragraph>
                {section.icon}
                {section.content}
              </Typography>
            )}
            {section.items && (
              <List dense sx={{ pl: 2 }}>
                {section.items.map((item, i) => (
                  <ListItem key={i} disablePadding>
                    <ListItemText primary={`• ${item}`} />
                  </ListItem>
                ))}
              </List>
            )}
          </Box>
        ))}

        {/* Footer */}
        <Typography
          variant="caption"
          display="block"
          align="right"
          color="text.secondary"
          mt={4}
        >
          Last updated: June 15, 2025
        </Typography>
      </Box>
    </Container>
  );
}
