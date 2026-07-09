import {
  Box,
  Button,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  TextField,
} from "@mui/material";
import { Form, Formik } from "formik";
import React from "react";
import * as Yup from "yup";
import emailjs from "@emailjs/browser";
import Snackbar from "@mui/material/Snackbar";
import Alert from "@mui/material/Alert";

const schema = Yup.object().shape({
  name: Yup.string().required().min(5).max(40),
  email: Yup.string().required().email("Invalid email address format").max(255),
  phoneNumber: Yup.string().required().min(8).max(14),
  findUs: Yup.string().required(),
  message: Yup.string().required(), // ✅ Fix casing
});

const serviceId = "service_a7w8vkh";
const templateId = "template_1to3q3d";
const publicKey = "_E9TACxOeItnE-fsw";

const FormContacts = () => {
  const [open, setOpen] = React.useState(false);

  const handleClick = () => {
    setOpen(true);
  };

  const handleClose = (event, reason) => {
    if (reason === "clickaway") {
      return;
    }

    setOpen(false);
  };
  const constDates = async (values, actions) => {
    console.log("Form values:", values); // ✅ Log actual form data
    var templateParams = {
      name: values.name,
      email: values.email,
      message: values.message,
      phoneNumber: values.phoneNumber,
      findUs: values.findUs,
    };
    emailjs.send(serviceId, templateId, templateParams, publicKey).then(
      (response) => {
        handleClick();
      },
      (error) => {
        console.log("FAILED...", error.text);
      }
    );
    actions.resetForm();
  };

  return (
    <Box
      sx={{
        flex: 1,
        p: 2,
        display: "flex",
        flexDirection: "column",
        gap: 3,
        justifyContent: "space-between",
      }}
      mt={2}
    >
      <Formik
        validationSchema={schema}
        onSubmit={constDates}
        initialValues={{
          name: "",
          email: "",
          phoneNumber: "",
          findUs: "",
          message: "",
        }}
      >
        {({
          handleSubmit,
          handleChange,
          handleBlur,
          values,
          touched,
          errors,
        }) => (
          <Form noValidate onSubmit={handleSubmit}>
            <TextField
              label="Name"
              name="name"
              value={values.name}
              onChange={handleChange}
              onBlur={handleBlur}
              variant="standard"
              fullWidth
              placeholder="John/Jane Doe"
              focused
              error={touched.name && !!errors.name}
              helperText={touched.name && errors.name}
            />

            <TextField
              sx={{ mt: 3 }}
              label="Email"
              name="email"
              type="email"
              value={values.email}
              onChange={handleChange}
              onBlur={handleBlur}
              variant="standard"
              fullWidth
              placeholder="name@company.com"
              focused
              error={touched.email && !!errors.email}
              helperText={touched.email && errors.email}
            />

            <TextField
              sx={{ mt: 3 }}
              label="Phone"
              name="phoneNumber"
              type="tel"
              value={values.phoneNumber}
              onChange={handleChange}
              onBlur={handleBlur}
              variant="standard"
              fullWidth
              placeholder="+1 (206) 987-6543"
              focused
              error={touched.phoneNumber && !!errors.phoneNumber}
              helperText={touched.phoneNumber && errors.phoneNumber}
            />

            <FormControl fullWidth focused variant="standard" sx={{ mt: 3 }}>
              <InputLabel id="select-find-us-label">
                How Did You Find Us
              </InputLabel>
              <Select
                labelId="select-find-us-label"
                id="select-find-us"
                name="findUs"
                value={values.findUs}
                onChange={handleChange}
                onBlur={handleBlur}
                error={touched.findUs && !!errors.findUs}
              >
                <MenuItem value="">
                  <em>None</em>
                </MenuItem>
                <MenuItem value="Friends">Friends</MenuItem>
                <MenuItem value="Our Website">Our Website</MenuItem>
                <MenuItem value="Marketplace">Marketplace</MenuItem>
              </Select>
            </FormControl>

            <TextField
              sx={{ mt: 3 }}
              label="Message"
              name="message"
              value={values.message}
              onChange={handleChange}
              onBlur={handleBlur}
              variant="outlined"
              fullWidth
              multiline
              rows={4}
              focused
              placeholder="Hello, I’m interested in..."
              error={touched.message && !!errors.message}
              helperText={touched.message && errors.message}
            />

            <Box sx={{ display: "flex", justifyContent: "center" }} mt={4}>
              <Button
                type="submit"
                variant="contained"
                color="primary"
                size="large"
              >
                Submit Message Now
              </Button>
              <Snackbar
                open={open}
                autoHideDuration={6000}
                onClose={handleClose}
                anchorOrigin={{ vertical: "top", horizontal: "right" }}
              >
                <Alert
                  onClose={handleClose}
                  severity="success"
                  variant="filled"
                  sx={{ width: "100%" }}
                >
                  Your message was send. We'll be in touch with you soon
                </Alert>
              </Snackbar>
            </Box>
          </Form>
        )}
      </Formik>
    </Box>
  );
};

export default FormContacts;
