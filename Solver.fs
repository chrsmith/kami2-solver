module PuzzleLoader.Solver

open System.Collections.Generic

open PuzzleLoader.Analysis

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
// live in Earth-1.

// $ brew install graphviz
// https://en.wikipedia.org/wiki/DOT_(graph_description_language)
// Best explanation

// Example:
// dot -T test.dot -o test.png

// Emit the dot file representation for a puzzle's regions/nodes.
let emitRegionLabels (regions : List<Region>) =
    regions
    |> Seq.map (fun region ->
        sprintf "    r_%d [style=\"filled\", fillcolor=\"%s\"]"
                region.ID
                region.ColorCode)

// Emit the dot file representation for a region's associations.
let emitRegionAssociations region =
    region.AdjacentRegions
    |> Seq.map (sprintf "r_%d")
    |> Seq.map (sprintf "r_%d -- %s" region.ID)


// Convert a Kami2 puzzle into a string dot file, to be rendered.
let ConvertToGraph (puzzle : Kami2Puzzle) =
    seq {
        yield "strict graph kami_puzzle {"
        // Emit the nodes. Give each a number (since puzzles with more than
        // 26 regions are common) and assign the source color.
        yield "    // edges"
        yield! emitRegionLabels puzzle.Regions
        yield "    // edges"
        yield! puzzle.Regions
               |> Seq.map emitRegionAssociations
               |> Seq.concat
               |> Seq.map (sprintf "    %s")
            
        yield "}"
    }
