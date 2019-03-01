﻿using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions.Internal;

namespace Microsoft.CodeAnalysis.Tools.Tests
{
    internal class TestLogger : ILogger
    {
        private readonly StringBuilder _builder = new StringBuilder();

        public IDisposable BeginScope<TState>(TState state)
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            _builder.AppendLine(message);
        }

        public string GetLog()
        {
            return _builder.ToString();
        }
    }
}
