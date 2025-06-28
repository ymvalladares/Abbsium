import * as yup from "yup";

const passwordRules = /^[A-Z][A-Za-z\d@$!%*?&]{4,}$/;
// min 5 characters, 1 upper case letter, 1 lower case letter, 1 numeric digit.

export const Schema_Login_Validation = yup.object().shape({
  email: yup.string().email("Please enter a valid email").required("Required"),
  username: yup.string().min(3, "Username must be at least 3 characters long"),
  password: yup
    .string()
    .min(5, "Password must be at least 5 characters")
    .matches(passwordRules, {
      message: "Password must start with a capital letter",
    }),
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
