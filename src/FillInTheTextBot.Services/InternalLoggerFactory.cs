﻿using Microsoft.Extensions.Logging;

namespace FillInTheTextBot.Services
{
    public static class InternalLoggerFactory
    {
        public static ILoggerFactory Factory { get; set; }

        public static ILogger CreateLogger<T>() => Factory?.CreateLogger<T>();

        public static ILogger CreateLogger(string categoryName) => Factory?.CreateLogger(categoryName);
    }
}
