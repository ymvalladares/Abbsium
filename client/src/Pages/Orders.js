import React, { useState } from "react";
import {
  Box,
  Button,
  Checkbox,
  IconButton,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TablePagination,
  useMediaQuery,
  TextField,
} from "@mui/material";
import { useTheme } from "@mui/material/styles";
import AddIcon from "@mui/icons-material/Add";
import SearchIcon from "@mui/icons-material/Search";
import Footer from "../Components/Footer";
import AutoAwesomeMosaicIcon from "@mui/icons-material/AutoAwesomeMosaic";
import DeleteForeverIcon from "@mui/icons-material/DeleteForever";

const orders = [
  {
    name: "Dagmar Garcia",
    producto: "Web Page",
    website: "innovuspro.com",
    dob: "29.03.2016",
    status: "Active",
    plan: "Premium",
  },
  {
    name: "Carlos Mendez",
    producto: "Web Page",
    website: "mendeztech.io",
    dob: "12.07.2015",
    status: "Active",
    plan: "Advance",
  },
  {
    name: "Lucia Herrera",
    producto: "Design",
    website: "N/A",
    dob: "03.11.2018",
    status: "Active",
    plan: "N/A",
  },
  {
    name: "Jorge Ramírez",
    producto: "Web Page",
    website: "buildstrong.org",
    dob: "21.06.2017",
    status: "Pending",
    plan: "Basic",
  },
  {
    name: "Sandra López",
    producto: "Design",
    website: "N/A",
    dob: "15.02.2019",
    status: "Active",
    plan: "N/A",
  },
  {
    name: "Kevin Torres",
    producto: "Web Page",
    website: "torresdevs.co",
    dob: "09.09.2020",
    status: "Inactive",
    plan: "Advance",
  },
  {
    name: "Natalia Ruiz",
    producto: "Web Page",
    website: "ruizmedia.net",
    dob: "28.01.2017",
    status: "Active",
    plan: "Basic",
  },
  {
    name: "Andres Vega",
    producto: "Web Page",
    website: "vegaconsulting.org",
    dob: "04.12.2016",
    status: "Inactive",
    plan: "Basic",
  },
];

const tableHeaders = [
  { key: "name", label: "Full name" },
  { key: "producto", label: "Producto" },
  { key: "website", label: "Website" },
  { key: "dob", label: "Date" },
  { key: "status", label: "Status", isChip: true },
  { key: "plan", label: "Plan" },
];

const Orders = () => {
  const [rows, setRows] = useState(orders);
  const [searchQuery, setSearchQuery] = useState("");
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(7);
  const [selectedRows, setSelectedRows] = useState([]);
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  const allRowNames = rows.map((row) => row.name);
  const isAllSelected = selectedRows.length === allRowNames.length;
  const isSomeSelected = selectedRows.length > 0 && !isAllSelected;

  const toggleSelectAll = () => {
    setSelectedRows(isAllSelected ? [] : allRowNames);
  };

  const toggleRowSelection = (name) => {
    setSelectedRows((prev) =>
      prev.includes(name) ? prev.filter((n) => n !== name) : [...prev, name]
    );
  };

  const handleSearch = (e) => {
    e.preventDefault();
    const filtered = orders.filter((row) =>
      row.name.toLowerCase().includes(searchQuery.toLowerCase())
    );
    setRows(filtered);
    setPage(0);
  };

  const handleChangePage = (event, newPage) => {
    setPage(newPage);
  };

  const handleChangeRowsPerPage = (event) => {
    setRowsPerPage(parseInt(event.target.value, 10));
    setPage(0);
  };

  return (
    <>
      <Box p={isMobile ? 2 : 4} mb={-5}>
        <Box
          display="flex"
          flexDirection={isMobile ? "column" : "row"}
          justifyContent="space-between"
          gap={2}
          mb={2}
          ml={1}
          mr={2}
        >
          <Box display="flex" flexWrap="wrap" gap={1}>
            <IconButton
              size="small"
              aria-label="notifications"
              aria-controls="menu-appbar"
              aria-haspopup="true"
              color="black"
              sx={{
                display: { xs: "none", sm: "flex" },
                width: 40,
                height: 40,
                alignItems: "center",
                justifyContent: "center",
                mr: 2,
                borderRadius: "8px",
                backgroundColor: "#E3F0FE",
                color: "#0399DF",
                transition: "0.3s",
                "&:hover": {
                  backgroundColor: "#0399DF",
                  color: "#E3F0FE",
                  transition: "0.5s",
                },
              }}
            >
              <AutoAwesomeMosaicIcon fontSize="small" />
            </IconButton>

            <Box
              component="form"
              onSubmit={handleSearch}
              style={{
                display: "flex",
                alignItems: "center",
                width: isMobile ? "100%" : 300,
              }}
            >
              <TextField
                size="small"
                autoComplete="off"
                onChange={(e) => setSearchQuery(e.target.value)}
                label="Search order by name"
                sx={{
                  "& .MuiFormLabel-root": {
                    fontSize: "13px",
                    marginTop: "2px",
                    color: "#0399DF",
                    fontWeight: "bold",
                  },
                  backgroundColor: "#FAFAFB",
                  borderRadius: "8px",
                  width: "100%",
                }}
                InputProps={{
                  endAdornment: (
                    <IconButton onClick={handleSearch} edge="end">
                      <SearchIcon />
                    </IconButton>
                  ),
                  sx: {
                    ".MuiOutlinedInput-notchedOutline": {
                      border: "2px solid #0399DF !important",
                      borderRadius: "8px",
                    },
                    "&:hover .MuiOutlinedInput-notchedOutline": {
                      border: "2px solid #0399DF !important",
                    },
                  },
                }}
              />
            </Box>
          </Box>
          <Box display="flex" alignItems="center" flexWrap="wrap" gap={3}>
            <Button
              sx={{
                textTransform: "none",
                backgroundColor: "#0399DF !important",
                borderRadius: "8px",
                marginTop: { xs: "10px", sm: "0" },
                border: "2px solid #0399DF",
                fontWeight: "bold",
              }}
              variant="contained"
              startIcon={<AddIcon />}
              fullWidth={isMobile}
            >
              Add Order
            </Button>
            <Button
              sx={{
                backgroundColor: "white",
                color: "#C62828",
                borderRadius: "8px",
                textTransform: "none",
                border: "2px solid #C62828",
                marginTop: { xs: "10px", sm: "0" },
                fontWeight: "bold",
                ":hover": {
                  backgroundColor: "white",
                },
              }}
              variant="contained"
              startIcon={<DeleteForeverIcon />}
              fullWidth={isMobile}
            >
              Delete
            </Button>
          </Box>
        </Box>

        <TableContainer>
          <Table sx={{ borderCollapse: "separate", borderSpacing: "0 6px" }}>
            <TableHead>
              <TableRow>
                <TableCell
                  padding="checkbox"
                  sx={{ border: "none", fontWeight: 600 }}
                >
                  <Checkbox
                    checked={isAllSelected}
                    indeterminate={isSomeSelected}
                    onChange={toggleSelectAll}
                  />
                </TableCell>
                {tableHeaders.map((col) => (
                  <TableCell
                    key={col.key}
                    sx={{ border: "none", fontWeight: 600 }}
                  >
                    {col.label}
                  </TableCell>
                ))}
              </TableRow>
            </TableHead>

            <TableBody>
              {rows
                .slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage)
                .map((row, index) => {
                  const isSelected = selectedRows.includes(row.name);
                  return (
                    <TableRow
                      key={index}
                      sx={{
                        transition: "all 0.3s ease",
                      }}
                    >
                      <TableCell
                        padding="checkbox"
                        sx={{
                          borderBottom: "none",
                          paddingTop: "5px",
                          paddingBottom: "5px",
                          fontSize: "0.875rem",
                          fontWeight: 400,
                          backgroundColor: isSelected ? "#E3F0FE" : "inherit",
                          borderRadius: isSelected ? "8px 0 0 8px" : 0,
                        }}
                      >
                        <Checkbox
                          checked={!!isSelected}
                          onChange={() => toggleRowSelection(row.name)}
                        />
                      </TableCell>

                      {tableHeaders.map((col, colIndex) => (
                        <TableCell
                          key={col.key}
                          sx={{
                            borderBottom: "none",
                            paddingTop: "5px",
                            paddingBottom: "5px",
                            fontSize: "0.875rem",
                            fontWeight: 400,
                            backgroundColor: isSelected ? "#E3F0FE" : "inherit",
                            borderRadius:
                              isSelected &&
                              ((colIndex === 0 &&
                                !colIndex === tableHeaders.length - 1) ||
                                colIndex === tableHeaders.length - 1)
                                ? colIndex === 0
                                  ? "8px 0 0 8px"
                                  : "0 8px 8px 0"
                                : 0,
                          }}
                        >
                          {col.isChip ? (
                            <Box
                              sx={{
                                display: "inline-block",
                                px: 1.5,
                                py: 0.5,
                                border:
                                  row[col.key] === "Active"
                                    ? "1px solid #28a745"
                                    : row[col.key] === "Pending"
                                      ? "1px solid #ffc107"
                                      : " 1px solid #C62828",
                                borderRadius: "16px",
                                color: "white",
                                fontSize: "0.75rem",
                                fontWeight: "bold",
                                backgroundColor:
                                  row[col.key] === "Active"
                                    ? "#28a745"
                                    : row[col.key] === "Pending"
                                      ? "#ffc107"
                                      : "#C62828",
                                whiteSpace: "nowrap",
                              }}
                            >
                              {row[col.key]}
                            </Box>
                          ) : (
                            row[col.key]
                          )}
                        </TableCell>
                      ))}
                    </TableRow>
                  );
                })}
            </TableBody>
          </Table>
          <TablePagination
            sx={{ mt: 4, mb: { xs: 8 } }}
            rowsPerPageOptions={[7, 10]}
            component="div"
            count={rows.length}
            rowsPerPage={rowsPerPage}
            page={page}
            onPageChange={handleChangePage}
            onRowsPerPageChange={handleChangeRowsPerPage}
          />
        </TableContainer>
      </Box>
      <Footer />
    </>
  );
};

export default Orders;
