using System;

namespace WpfDigitalClockAlarm.Tests
{
    public static class Assert
    {
        public static void True(bool condition, string? message = null)
        {
            if (!condition)
                throw new Exception($"Assertion Failed: Expected True, got False. {message}");
        }

        public static void False(bool condition, string? message = null)
        {
            if (condition)
                throw new Exception($"Assertion Failed: Expected False, got True. {message}");
        }

        public static void Equal<T>(T expected, T actual, string? message = null)
        {
            if (!Equals(expected, actual))
                throw new Exception($"Assertion Failed: Expected <{expected}>, but got <{actual}>. {message}");
        }

        public static void NotNull(object? obj, string? message = null)
        {
            if (obj == null)
                throw new Exception($"Assertion Failed: Expected non-null object. {message}");
        }
    }
}
