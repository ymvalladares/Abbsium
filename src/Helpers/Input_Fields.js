import { InputAdornment, TextField, Typography } from "@mui/material";
import { useField } from "formik";
import Visibility from "@mui/icons-material/Visibility";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import React from "react";

const Input_Fields = ({ label, ...props }) => {
  const [field, meta] = useField(props);
  //console.log(props);
  const [showPassword, setShowPassword] = React.useState(true);

  const handleClickShowPassword = () => setShowPassword((show) => !show);

  return (
    <>
      <TextField
        {...field}
        type={props.type == "password" && showPassword ? "password" : "text"}
        id={props.key}
        size="small"
        focused
        required
        error={meta.touched && meta.error ? true : false}
        label={label}
        sx={{
          padding: "4px",
        }}
        InputProps={
          props.type == "password"
            ? {
                endAdornment: (
                  <InputAdornment
                    sx={{
                      cursor: "pointer",
                    }}
                    position="end"
                    onClick={handleClickShowPassword}
                  >
                    {!showPassword ? <Visibility /> : <VisibilityOff />}
                  </InputAdornment>
                ),
              }
            : {}
        }
      />
      {meta.touched && meta.error && (
        <Typography
          variant="caption"
          fontWeight="bold"
          sx={{ textAlign: "center", color: "red" }}
        >
          {meta.error}
        </Typography>
      )}
      {/* <label>{label}</label>
      <input
        {...field}
        {...props}
        style={{
          padding: "8px",
          marginBottom: "8px",
          borderRadius: "6px",
          border: "1px solid var(--border)",
          background: "var(--background)",
          color: "var(--text-primary)",
        }}
        className={meta.touched && meta.error ? "input-error" : ""}
        required
      />
      {meta.touched && meta.error && <div className="error">{meta.error}</div>} */}
    </>
  );
};
export default Input_Fields;
