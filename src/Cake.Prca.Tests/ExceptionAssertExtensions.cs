﻿namespace Cake.Prca.Tests
{
    using System;
    using Xunit;

    public static class ExceptionAssertExtensions
    {
        public static void IsArgumentException(this Exception exception, string parameterName)
        {
            Assert.IsType<ArgumentException>(exception);
            Assert.Equal(parameterName, ((ArgumentException)exception).ParamName);
        }

        public static void IsArgumentNullException(this Exception exception, string parameterName)
        {
            Assert.IsType<ArgumentNullException>(exception);
            Assert.Equal(parameterName, ((ArgumentNullException)exception).ParamName);
        }

        public static void IsArgumentOutOfRangeException(this Exception exception, string parameterName)
        {
            Assert.IsType<ArgumentOutOfRangeException>(exception);
            Assert.Equal(parameterName, ((ArgumentOutOfRangeException)exception).ParamName);
        }

        public static void IsInvalidOperationException(this Exception exception, string message)
        {
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Equal(message, exception.Message);
        }

        public static void IsPrcaException(this Exception exception, string message)
        {
            Assert.IsType<PrcaException>(exception);
            Assert.Equal(message, exception.Message);
        }
    }
}
