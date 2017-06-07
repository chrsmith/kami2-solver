module PuzzleLoader.Solver

// FSharp.Charting. Cool for 2D charts.
// https://fslab.org/FSharp.Charting/
// https://github.com/fslaborg/FSharp.Charting

// Several other good graph rendering libraries for
// .NET, but sadly all require Windows. None I could
// find for .NET Core.

// http://www.graphviz.org is my go-to
// C# wrapper, cool.
// https://github.com/JamieDixon/GraphViz-C-Sharp-Wrapper
// https://www.nuget.org/packages/GraphViz.NET/

// We'll just hard-code graphviz and shell out to it.
// In a parallel universe where I have more free time,
// building a proper NuGet package that distributes
// GraphViz with the bits, and/or embeds the binary
// into the .NET assembly would be nice. But alas, we
// live in Earth 1.