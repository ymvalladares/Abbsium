import React, { useEffect, useState } from "react";
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
import { Get_Request } from "../Services/PaymentService";

const users = [
  {
    id: "1f4a92",
    username: "lunafox",
    email: "luna.fox@example.com",
    emailConfirmed: true,
    status: "Active",
  },
  {
    id: "9d3c01",
    username: "tigerbyte",
    email: "tiger.byte@example.com",
    emailConfirmed: false,
    status: "Pending",
  },
  {
    id: "7a1fbc",
    username: "skynova",
    email: "nova.sky@example.com",
    emailConfirmed: true,
    status: "Active",
  },
  {
    id: "c3e882",
    username: "echohawk",
    email: "hawk.echo@example.com",
    emailConfirmed: false,
    status: "Suspended",
  },
  {
    id: "a7b902",
    username: "blueember",
    email: "blue.ember@example.com",
    emailConfirmed: true,
    status: "Active",
  },
  {
    id: "68fe0a",
    username: "nightshade",
    email: "night.shade@example.com",
    emailConfirmed: false,
    status: "Pending",
  },
  {
    id: "d5cf3e",
    username: "zephyrwave",
    email: "zephyr.wave@example.com",
    emailConfirmed: true,
    status: "Active",
  },
];

const tableHeaders = [
  { key: "id", label: "Id" },
  { key: "username", label: "Username" },
  { key: "email", label: "Email" },
  { key: "emailConfirmed", label: "Email Confirmed" },
  { key: "status", label: "Status", isChip: true },
];

const Users = () => {
  const [rows, setRows] = useState([]);
  const [searchQuery, setSearchQuery] = useState("");
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(7);
  const [selectedRows, setSelectedRows] = useState([]);
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  const allRowIds = rows.map((row) => row.id);
  const isAllSelected = selectedRows.length === allRowIds.length;
  const isSomeSelected = selectedRows.length > 0 && !isAllSelected;

  useEffect(() => {
    const fetchUsers = async () => {
      try {
        const response = await Get_Request(`${window.BaseUrlGeneral}User`); // Ajusta la ruta si es diferente
        console.log(response.data);
        setRows(response.data);
      } catch (error) {
        console.error("Error al obtener usuarios:", error);
      }
    };

    fetchUsers();
  }, []);

  const toggleSelectAll = () => {
    setSelectedRows(isAllSelected ? [] : allRowIds);
  };

  const toggleRowSelection = (id) => {
    setSelectedRows((prev) =>
      prev.includes(id) ? prev.filter((n) => n !== id) : [...prev, id]
    );
  };

  const handleSearch = (e) => {
    e.preventDefault();
    const filtered = users.filter((row) =>
      row.username.toLowerCase().includes(searchQuery.toLowerCase())
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
          <Table sx={{ borderCollapse: "separate", buserspacing: "0 6px" }}>
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
                  const isSelected = selectedRows.includes(row.id);
                  return (
                    <TableRow key={index} sx={{ transition: "all 0.3s ease" }}>
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
                          onChange={() => toggleRowSelection(row.id)}
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
                                      : "1px solid #C62828",
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
                          ) : col.key === "emailConfirmed" ? (
                            row[col.key] ? (
                              "Yes"
                            ) : (
                              "No"
                            )
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

export default Users;
