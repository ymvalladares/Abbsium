import {
  Box,
  Typography,
  Divider,
  List,
  ListItem,
  ListItemText,
  Container,
} from "@mui/material";
import GppGoodIcon from "@mui/icons-material/GppGood";
import PublicIcon from "@mui/icons-material/Public";
import EmailIcon from "@mui/icons-material/Email";
import AccessTimeIcon from "@mui/icons-material/AccessTime";

export default function PrivacyPolicy() {
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
          <GppGoodIcon color="primary" fontSize="large" />
          <Typography variant="h3" fontWeight="bold">
            Privacy Policy
          </Typography>
        </Box>

        <Typography variant="body1" paragraph>
          At <strong>Abbsium</strong>, your privacy matters. This policy
          explains how we collect, use, and protect your data when you access
          our website and services.
        </Typography>

        <Divider sx={{ my: 3 }} />

        {/* Sections */}
        {[
          {
            title: "1. Information We Collect",
            items: [
              "Name, email, and phone number",
              "Business and payment information",
              "Technical details (IP address, browser, device)",
              "Browsing behavior (via cookies)",
            ],
          },
          {
            title: "2. Legal Basis for Processing",
            content:
              "We process your information only when we have a legal basis, such as your consent, performance of a contract, or compliance with legal obligations.",
          },
          {
            title: "3. How We Use Your Data",
            items: [
              "To deliver and maintain services",
              "To process billing and subscriptions",
              "To communicate updates and marketing (opt-out available)",
              "To analyze usage and improve platform features",
            ],
          },
          {
            title: "4. Data Retention",
            content:
              "We retain your personal data only as long as necessary to fulfill the purposes outlined here or as required by law.",
            icon: <AccessTimeIcon fontSize="small" sx={{ mr: 1 }} />,
          },
          {
            title: "5. Sharing Your Information",
            items: [
              "With trusted service providers under contract",
              "With payment processors for secure transactions",
              "With law enforcement when legally required",
            ],
          },
          {
            title: "6. Data Security",
            content:
              "We use encryption, firewalls, and secure servers to protect your information. Access is restricted only to authorized personnel.",
          },
          {
            title: "7. International Transfers",
            content:
              "Your data may be transferred to countries outside your own. In such cases, we ensure appropriate safeguards are in place.",
            icon: <PublicIcon fontSize="small" sx={{ mr: 1 }} />,
          },
          {
            title: "8. Your Rights",
            items: [
              "Access, correct, or delete your personal data",
              "Withdraw consent at any time",
              "Lodge complaints with data protection authorities",
            ],
          },
          {
            title: "9. Cookies and Tracking",
            content:
              "We use cookies for performance and personalization. You can disable cookies in your browser settings.",
          },
          {
            title: "10. Children's Privacy",
            content:
              "Our services are not directed to children under 13. We do not knowingly collect data from minors.",
          },
          {
            title: "11. Third-Party Links",
            content:
              "We may link to external sites. We are not responsible for their content or privacy policies.",
          },
          {
            title: "12. Changes to This Policy",
            content:
              "This policy may be updated. Changes will be posted here with a new 'Last Updated' date.",
          },
          {
            title: "13. Contact Us",
            content: (
              <>
                <Box display="flex" alignItems="center" mb={1}>
                  <EmailIcon fontSize="small" sx={{ mr: 1 }} />
                  <Typography variant="body1">
                    <strong>yordan.j.martinez@gmail.com</strong>
                  </Typography>
                </Box>
              </>
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
