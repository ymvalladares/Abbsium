import React, { useMemo, useState } from "react";
import { Formik, Form } from "formik";
import { Schema_Login_Validation } from "../Helpers/SchemaValidation";
import Input_Fields from "../Helpers/Input_Fields";
import CustomCheckbox from "../Helpers/CustomCheckbox";
import { Box, Button, Chip, Divider, Stack, Typography } from "@mui/material";
import GoogleIcon from "@mui/icons-material/Google";
import Alerts from "../ReusableComp/Alerts";
import { Link, useNavigate } from "react-router-dom";
import "./Login.css";

const FORM_FIELDS = [
  {
    name: "email",
    label: "E-mail",
    type: "email",
    action: ["login", "register", "forgetPassword"],
  },
  { name: "username", label: "Username", type: "text", action: ["register"] },
  {
    name: "password",
    label: "Password",
    type: "password",
    action: ["login", "register"],
  },
];

const Login = () => {
  const [userAction, setUserAction] = useState("login");
  const [alert, setAlert] = useState({ message: "", severity: "" });
  const navigate = useNavigate();

  const filteredInputs = useMemo(
    () => FORM_FIELDS.filter((field) => field.action.includes(userAction)),
    [userAction]
  );

  const handleSubmit = async (values, actions) => {
    const userData = {
      name: "Yordan",
      email: "yordan@abbsium.com",
      image: "https://avatars.githubusercontent.com/u/103406224",
    };

    localStorage.setItem("session", JSON.stringify({ user: userData }));
    navigate("/dashboard");
    // Simulated API response placeholder
    /*
    Pots_Request(`${window.BaseUrlGeneral}Account/${userAction}`, values)
      .then(response => {
        const { status, data } = response;
        if (status === "200") {
          if (userAction === "login") {
            localStorage.setItem("TOKEN_KEY", JSON.stringify(data.message));
            actions.resetForm();
            navigate("/home-page", { state: { email: values.email } });
          } else {
            actions.resetForm();
            setAlert({
              message: userAction === "register" ? "User Created Successfully" : "Email sent",
              severity: "success",
            });
          }
        }
      })
      .catch(error => {
        setAlert({ message: error.response?.data || "Something went wrong", severity: "error" });
        actions.resetForm();
      });
    */
  };

  const renderToggleAuthLink = () => {
    const isLogin = userAction === "login";
    return (
      <Typography
        variant="caption"
        onClick={() => {
          setUserAction(isLogin ? "register" : "login");
          setAlert({ message: "" });
        }}
        sx={{ fontWeight: "bold", cursor: "pointer" }}
      >
        {isLogin ? "Don’t have an account?" : "Already have an account?"}
      </Typography>
    );
  };

  return (
    <Box
      display="flex"
      justifyContent="center"
      alignItems="center"
      textAlign="center"
      sx={{ minHeight: { xs: "65vh", md: "90vh" } }}
      px={2} // padding horizontal para móviles
    >
      <Box
        sx={{
          display: "flex",
          flexDirection: "column",
          padding: { xs: 3, sm: 4 },
          boxShadow: "0px 0px 15px rgba(0, 0, 255, 0.3)",
          borderRadius: "12px",
          width: {
            xs: "100%", // 100% del ancho en móviles
            sm: "400px", // en tablets
            md: "420px", // en desktop medianos
            lg: "450px", // en pantallas grandes
          },
          backgroundColor: "#fff", // opcional para claridad
        }}
      >
        <Stack alignItems="center" width="100%">
          <Typography color="secondary.main" variant="h5" fontWeight="bold">
            {userAction === "login" ? "Sign In Abbsium" : "Sign up Abbsium"}
          </Typography>
          <Typography variant="caption" fontSize="16px" textAlign="center">
            Enter your credentials to continue
          </Typography>
        </Stack>

        <Button
          fullWidth
          variant="outlined"
          disableElevation
          sx={{
            color: "grey.700",
            backgroundColor: "white",
            borderColor: "grey",
            mt: 1,
            textTransform: "none",
          }}
        >
          <GoogleIcon sx={{ color: "red", mr: 2, fontSize: "18px" }} />
          {userAction === "login"
            ? "Sign in with Google"
            : "Sign up with Google"}
        </Button>

        <Divider sx={{ my: 3 }}>
          <Chip label="OR" variant="outlined" sx={{ color: "blue", px: 2 }} />
        </Divider>

        {alert.message && (
          <Alerts severity={alert.severity} title={alert.message} />
        )}

        <Formik
          initialValues={{ email: "", password: "", username: "" }}
          validationSchema={Schema_Login_Validation}
          onSubmit={handleSubmit}
        >
          {({ isSubmitting }) => (
            <Form>
              {filteredInputs.map(({ name, label, type }) => (
                <Input_Fields
                  key={name}
                  name={name}
                  label={label}
                  type={type}
                />
              ))}

              {userAction === "login" && (
                <CustomCheckbox name="remember_me" type="checkbox" />
              )}

              <Button
                type="submit"
                fullWidth
                variant="contained"
                disabled={isSubmitting}
                sx={{
                  mt: 2,
                  mb: 1,
                  py: 1,
                  fontWeight: 600,
                  textTransform: "none",
                  backgroundColor: "#8e05c2 !important",
                  border: "1px solid #8e05c2",
                }}
              >
                {
                  {
                    login: "Log In",
                    register: "Create",
                    forgetPassword: "Send Email",
                  }[userAction]
                }
              </Button>
            </Form>
          )}
        </Formik>

        {userAction === "login" && (
          <Box display="flex" justifyContent="space-between" width="100%">
            <Typography variant="caption" fontWeight="bold">
              Forget your password?
            </Typography>
            <Link
              style={{
                color: "#8e05c2",
                fontWeight: "bold",
                cursor: "pointer",
              }}
              onClick={() => {
                setUserAction("forgetPassword");
                setAlert({ message: "" });
              }}
            >
              Reset Password
            </Link>
          </Box>
        )}

        <Divider sx={{ my: 3 }} />
        {renderToggleAuthLink()}
      </Box>
    </Box>
  );
};

export default Login;
