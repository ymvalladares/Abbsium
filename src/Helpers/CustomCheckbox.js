import { Box, Typography } from "@mui/material";
import { useField } from "formik";

const CustomCheckbox = ({ label, ...props }) => {
  const [field, meta] = useField(props);

  return (
    <>
      <Box sx={{ display: "flex", marginTop: "7px" }}>
        <input {...field} {...props} />
        <Typography variant="caption">Remember Me</Typography>
      </Box>

      {/* {meta.touched && meta.error && <div className="error">{meta.error}</div>} */}
    </>
  );
};
export default CustomCheckbox;
