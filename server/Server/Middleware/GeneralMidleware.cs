﻿using Server.Entitys;
using System.Net;
using System.Text.Json;

namespace Server.Middleware
{
    public class GeneralMidleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GeneralMidleware> _logger;
        private readonly IHostEnvironment _env;

        public GeneralMidleware(RequestDelegate next, ILogger<GeneralMidleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Request aqui
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                var response = _env.IsDevelopment()
                    ? new ApiException(context.Response.StatusCode, ex.Message, ex.StackTrace?.ToString())
                    : new ApiException(context.Response.StatusCode, "Internal Server Error");

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

                var json = JsonSerializer.Serialize(response, options);

                await context.Response.WriteAsync(json);
            }
        }
    }
}
