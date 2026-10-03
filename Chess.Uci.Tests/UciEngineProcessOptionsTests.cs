// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Chess.Uci.Tests;

public sealed class UciEngineProcessOptionsTests
{
    [Fact]
    public void Constructor_PreservesValuesAndDefaults()
    {
        var options = new UciEngineProcessOptions(
            "engine",
            ["first", "argument with spaces"],
            "working");

        Assert.Equal("engine", options.FileName);
        Assert.Equal(["first", "argument with spaces"], options.Arguments);
        Assert.Equal("working", options.WorkingDirectory);
        Assert.Equal(TimeSpan.FromSeconds(10), options.ResponseTimeout);
        Assert.Equal(TimeSpan.FromSeconds(2), options.ShutdownTimeout);
    }

    [Fact]
    public void Constructor_PreservesCustomTimeouts()
    {
        var options = new UciEngineProcessOptions(
            "engine",
            responseTimeout: TimeSpan.FromSeconds(3),
            shutdownTimeout: TimeSpan.FromSeconds(4));

        Assert.Equal(TimeSpan.FromSeconds(3), options.ResponseTimeout);
        Assert.Equal(TimeSpan.FromSeconds(4), options.ShutdownTimeout);
    }

    [Fact]
    public void Constructor_DefensivelyCopiesArguments()
    {
        var arguments = new List<string> { "first" };
        var options = new UciEngineProcessOptions("engine", arguments);

        arguments[0] = "changed";
        arguments.Add("second");

        Assert.Equal(["first"], options.Arguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsInvalidFileName(
        string? fileName)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new UciEngineProcessOptions(fileName!));
    }

    [Fact]
    public void Constructor_RejectsNullArgumentEntry()
    {
        Assert.Throws<ArgumentException>(() =>
            new UciEngineProcessOptions("engine", [null!]));
    }

    [Fact]
    public void Constructor_DoesNotRequireExecutableToExist()
    {
        var options = new UciEngineProcessOptions(
            "an executable that does not exist");

        Assert.Equal("an executable that does not exist", options.FileName);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveResponseTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UciEngineProcessOptions(
                "engine",
                responseTimeout: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UciEngineProcessOptions(
                "engine",
                responseTimeout: TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveShutdownTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UciEngineProcessOptions(
                "engine",
                shutdownTimeout: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UciEngineProcessOptions(
                "engine",
                shutdownTimeout: TimeSpan.FromTicks(-1)));
    }
}
