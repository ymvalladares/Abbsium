import React from "react";
import {
  Box,
  Grid,
  Card,
  CardContent,
  Typography,
  Chip,
  Button,
  Stack,
  Divider,
} from "@mui/material";
import AttachMoneyIcon from "@mui/icons-material/AttachMoney";
import TrendingUpIcon from "@mui/icons-material/TrendingUp";
import ShoppingCartIcon from "@mui/icons-material/ShoppingCart";
import Inventory2Icon from "@mui/icons-material/Inventory2";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
  ResponsiveContainer,
} from "recharts";

const AdminSite = () => {
  const data = [
    { fecha: "13/10", ingresos: 0, ganancias: 0 },
    { fecha: "14/10", ingresos: 0, ganancias: 0 },
    { fecha: "15/10", ingresos: 0, ganancias: 0 },
    { fecha: "16/10", ingresos: 0, ganancias: 0 },
    { fecha: "17/10", ingresos: 0, ganancias: 0 },
    { fecha: "18/10", ingresos: 0, ganancias: 0 },
    { fecha: "19/10", ingresos: 15, ganancias: 7 },
  ];

  return (
    <Box sx={{ p: 3, bgcolor: "#f5f8fc", minHeight: "100vh" }}>
      <Typography variant="h4" fontWeight="bold">
        Dashboard
      </Typography>
      <Typography variant="subtitle1" color="text.secondary" mb={3}>
        Panel de control y análisis de ventas
      </Typography>

      {/* Selector de fecha */}
      <Stack direction="row" spacing={1} mb={3}>
        <Button
          variant="contained"
          color="primary"
          sx={{ textTransform: "none" }}
        >
          Hoy
        </Button>
        <Button
          variant="outlined"
          color="primary"
          sx={{ textTransform: "none" }}
        >
          Esta Semana
        </Button>
        <Button
          variant="outlined"
          color="primary"
          sx={{ textTransform: "none" }}
        >
          Este Mes
        </Button>
      </Stack>

      {/* Tarjetas principales */}
      <Grid container spacing={2} mb={3}>
        <Grid item xs={12} sm={6} md={3}>
          <Card sx={{ borderRadius: 3, boxShadow: 2 }}>
            <CardContent>
              <Stack direction="row" alignItems="center" spacing={2}>
                <AttachMoneyIcon sx={{ fontSize: 35, color: "#1E88E5" }} />
                <Box>
                  <Typography variant="h6" fontWeight="bold">
                    $15.00
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    Ingresos
                  </Typography>
                  <Typography color="success.main" fontSize={13}>
                    +12%
                  </Typography>
                </Box>
              </Stack>
            </CardContent>
          </Card>
        </Grid>

        <Grid item xs={12} sm={6} md={3}>
          <Card sx={{ borderRadius: 3, boxShadow: 2 }}>
            <CardContent>
              <Stack direction="row" alignItems="center" spacing={2}>
                <TrendingUpIcon sx={{ fontSize: 35, color: "#2E7D32" }} />
                <Box>
                  <Typography variant="h6" fontWeight="bold">
                    $7.00
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    Ganancias
                  </Typography>
                  <Typography color="success.main" fontSize={13}>
                    +8%
                  </Typography>
                </Box>
              </Stack>
            </CardContent>
          </Card>
        </Grid>

        <Grid item xs={12} sm={6} md={3}>
          <Card sx={{ borderRadius: 3, boxShadow: 2 }}>
            <CardContent>
              <Stack direction="row" alignItems="center" spacing={2}>
                <ShoppingCartIcon sx={{ fontSize: 35, color: "#7E57C2" }} />
                <Box>
                  <Typography variant="h6" fontWeight="bold">
                    1
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    Ventas
                  </Typography>
                </Box>
              </Stack>
            </CardContent>
          </Card>
        </Grid>

        <Grid item xs={12} sm={6} md={3}>
          <Card sx={{ borderRadius: 3, boxShadow: 2 }}>
            <CardContent>
              <Stack direction="row" alignItems="center" spacing={2}>
                <Inventory2Icon sx={{ fontSize: 35, color: "#EF6C00" }} />
                <Box>
                  <Typography variant="h6" fontWeight="bold">
                    1
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    Productos Vendidos
                  </Typography>
                </Box>
              </Stack>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {/* Gráfica + panel lateral */}
      <Grid container spacing={3}>
        <Grid item xs={12} md={8}>
          <Card sx={{ borderRadius: 3, boxShadow: 2 }}>
            <CardContent>
              <Typography variant="h6" fontWeight="bold" mb={2}>
                Evolución de Ventas
              </Typography>
              <ResponsiveContainer width="100%" height={250}>
                <LineChart data={data}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="fecha" />
                  <YAxis />
                  <Tooltip />
                  <Line
                    type="monotone"
                    dataKey="ingresos"
                    stroke="#1E88E5"
                    strokeWidth={2}
                    name="Ingresos"
                  />
                  <Line
                    type="monotone"
                    dataKey="ganancias"
                    stroke="#2E7D32"
                    strokeWidth={2}
                    name="Ganancias"
                  />
                </LineChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Grid>

        <Grid item xs={12} md={4}>
          <Stack spacing={2}>
            {/* Alertas de Stock */}
            <Card sx={{ borderRadius: 3, boxShadow: 2 }}>
              <CardContent>
                <Stack direction="row" alignItems="center" spacing={1} mb={1}>
                  <WarningAmberIcon sx={{ color: "#EF6C00" }} />
                  <Typography variant="h6" fontWeight="bold">
                    Alertas de Stock
                  </Typography>
                </Stack>
                <Box>
                  <Typography variant="body1">Mouse Logitech</Typography>
                  <Typography variant="body2" color="text.secondary">
                    Stock: 1
                  </Typography>
                  <Chip
                    label="Bajo"
                    size="small"
                    color="warning"
                    sx={{ mt: 0.5 }}
                  />
                </Box>
                <Divider sx={{ my: 1 }} />
                <Box>
                  <Typography variant="body1">Cuaderno A4</Typography>
                  <Typography variant="body2" color="text.secondary">
                    Stock: 8
                  </Typography>
                  <Chip
                    label="Bajo"
                    size="small"
                    color="warning"
                    sx={{ mt: 0.5 }}
                  />
                </Box>
              </CardContent>
            </Card>

            {/* Top Productos */}
            <Card sx={{ borderRadius: 3, boxShadow: 2 }}>
              <CardContent>
                <Typography variant="h6" fontWeight="bold" mb={1}>
                  Top Productos
                </Typography>
                <Stack direction="row" justifyContent="space-between">
                  <Typography>1. Mouse Logitech</Typography>
                  <Typography color="success.main">$15.00</Typography>
                </Stack>
                <Typography variant="body2" color="text.secondary">
                  1 vendido
                </Typography>
              </CardContent>
            </Card>
          </Stack>
        </Grid>
      </Grid>
    </Box>
  );
};

export default AdminSite;
