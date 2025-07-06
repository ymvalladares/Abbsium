import React, { useMemo, useState } from "react";
import { Formik, Form } from "formik";
import { Schema_Login_Validation } from "../Helpers/SchemaValidation";
import Input_Fields from "../Helpers/Input_Fields";
import CustomCheckbox from "../Helpers/CustomCheckbox";
import { Box, Button, Chip, Divider, Stack, Typography } from "@mui/material";
import Alerts from "../ReusableComp/Alerts";
import { Link, useNavigate } from "react-router-dom";
import "./Login.css";
import Google_logo from "../Pictures/google-logo.png";
import { Pots_Request } from "../Services/PaymentService";
import { BeatLoader } from "react-spinners";
import { GoogleLogin } from "@react-oauth/google";

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
    console.log(values);
    try {
      const response = await Pots_Request(
        `${window.BaseUrlGeneral}Account/${userAction}`,
        values
      );

      if (response.status === 200) {
        if (userAction === "login") {
          const userData = {
            name: response.data.userName,
            email: response.data.email,
            image: "https://avatar.iran.liara.run/public/25", // avatar fijo
          };

          localStorage.setItem("session", JSON.stringify({ user: userData }));
          localStorage.setItem(
            "TOKEN_KEY",
            JSON.stringify(response.data.token)
          );

          navigate("/dashboard");
          actions.resetForm();

          setTimeout(() => {
            window.location.reload();
          }, 500);
        } else if (userAction === "register") {
          // Mostrar alerta de éxito
          setAlert({
            message: "User Created Successfully",
            severity: "success",
          });

          // Limpiar formulario después del registro
          actions.resetForm();
        } else if (userAction === "forgetPassword") {
          setAlert({
            message: "Password reset link sent to your email.",
            severity: "success",
          });

          actions.resetForm();

          // No redireccionamos ni recargamos
        }
      }
    } catch (error) {
      console.log(error);
      const msg = error.response?.data.message;

      setAlert({
        message: msg,
        severity: "error",
      });
    } finally {
      actions.setSubmitting(false);
    }
  };

  const handleGoogleLogin = async (credentialResponse) => {
    try {
      const { credential } = credentialResponse;

      if (!credential) {
        throw new Error("No se recibió el token de Google");
      }

      const response = await Pots_Request(
        `${window.BaseUrlGeneral}Account/google-login`,
        credential
      );

      if (!response || !response.data) {
        throw new Error("Respuesta inválida del servidor");
      }

      const user = response.data;

      localStorage.setItem("session", JSON.stringify({ user }));
      localStorage.setItem("TOKEN_KEY", JSON.stringify(user.token));

      console.log("Login con Google exitoso");
    } catch (error) {
      console.log(error);
      const msg = error.response?.data.message;

      setAlert({
        message: msg,
        severity: "error",
      });
    }
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
      sx={{ minHeight: { xs: "80vh", md: "90vh" } }}
      px={2} // padding horizontal para móviles
    >
      <Box
        sx={{
          display: "flex",
          flexDirection: "column",
          padding: { xs: 3, sm: 4 },
          boxShadow: "0px 0px 5px 5px #0399DF",
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
          <Typography color="#0399DF" variant="h5" fontWeight="bold">
            {userAction === "login" ? "Sign In Abbsium" : "Sign up Abbsium"}
          </Typography>
          <Typography
            variant="caption"
            fontSize="16px"
            textAlign="center"
            mb={3}
          >
            Enter your credentials to continue
          </Typography>
        </Stack>

        <GoogleLogin
          onSuccess={handleGoogleLogin}
          onError={() => console.log("Login Failed")}
          locale="en"
        />

        <Divider sx={{ my: 3 }}>
          <Chip
            label="OR"
            variant="outlined"
            sx={{
              color: "#0399DF",
              px: 2,
              fontWeight: "bold",
              border: "1px solid  #0399DF",
            }}
          />
        </Divider>

        {alert.message && (
          <Alerts severity={alert.severity} title={alert.message} />
        )}

        <Formik
          //key={userAction}
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
                  backgroundColor: "#0399DF !important",
                  border: "1px solid #0399DF",
                }}
              >
                {isSubmitting ? (
                  <BeatLoader
                    color="white"
                    //loading={loading}
                    //cssOverride={override}
                    size={18}
                    aria-label="Loading Spinner"
                    data-testid="loader"
                  />
                ) : (
                  {
                    login: "Log In",
                    register: "Create",
                    forgetPassword: "Send Email",
                  }[userAction]
                )}
              </Button>
            </Form>
          )}
        </Formik>

        {userAction === "login" && (
          <Box display="flex" justifyContent="space-between" width="100%">
            <Typography variant="caption" fontWeight="bold">
              Forget your password?
            </Typography>
            <Typography
              sx={{
                color: "#0399DF",
                fontWeight: "bold",
                cursor: "pointer",
                fontSize: 13,
              }}
              onClick={() => {
                setUserAction("forgetPassword");
                setAlert({ message: "" });
              }}
            >
              Reset Password
            </Typography>
          </Box>
        )}

        <Divider sx={{ my: 3 }} />
        {renderToggleAuthLink()}
      </Box>
    </Box>
  );
};

export default Login;
