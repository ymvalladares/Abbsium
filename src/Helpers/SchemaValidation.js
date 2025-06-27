import * as yup from "yup";

const passwordRules = /^(?=.*\d)(?=.*[a-z])(?=.*[A-Z]).{5,}$/;
// min 5 characters, 1 upper case letter, 1 lower case letter, 1 numeric digit.

export const Schema_Login_Validation = yup.object().shape({
  email: yup.string().email("Please enter a valid email").required("Required"),
  username: yup.string().min(3, "Username must be at least 3 characters long"),
  password: yup
    .string()
    .min(5)
    .matches(passwordRules, { message: "Use a better pasword" }),
  // remember_me: yup.boolean()
  // .oneOf([true], "Please accept the terms of service"),
});

export const Schema_Reset_Password_Validation = yup.object().shape({
  email: yup.string().email("Please enter a valid email").required(),
  newPassword: yup
    .string()
    .min(5)
    .matches(passwordRules, { message: "Use a better pasword" })
    .required(),

  confirmPassword: yup
    .string()
    .oneOf([yup.ref("newPassword"), null], "Passwords must match")
    .required(),
});
