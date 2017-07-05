namespace Kami2Solver.Types

open System.Collections.Generic


// Kami2 puzzle represented as a graph.
type Region = {
    ID: int
    Color: int
    // col, row position of a triangle in the region.
    Position: int * int
    // Hex value of the RGB value of the color index.
    ColorCode: string
    // Number of triangles in the region.
    Size: int
    // IDs of adjacent regions
    AdjacentRegions: Set<int>
} with
    override this.ToString() =
        sprintf
            "[%d][c%d] -> %A"
            this.ID this.Color (Set.ofSeq this.AdjacentRegions)


type Color = { Red: byte; Green: byte; Blue: byte }


// Raw puzzle extracted from a screenshot. Exported by the Analysis module.
type RawKami2Puzzle = {
    // Number of colors used in the puzzle.
    NumColors: int
    // RGB of the colors used.
    PuzzleColors: Color[]

    // Index of the color of each individual triagle. See other comments for
    // translating coordinates to triangles, and adjacency rules.
    // Has value of -1 if the triangle doesn't match any puzzle colors.
    Triangles: int[,]
}
         


// Kami2 puzzle in the process of being solved.
// Using the immutable collection types so they can be reused
// while doing tree search for a puzzle solution.
// https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-collection-types
type Kami2Puzzle = {
    Colors: Color[]
    Regions: seq<Region>
}


type Kami2PuzzleStep = {
    Regions: Map<int, Region>
} with
    member this.IsSolved =
        let firstRegionColor =
            this.Regions
            |> Map.toSeq
            |> Seq.head
            |> (fun (_,region) -> region.Color)

        this.Regions
        |> Map.forall (fun _ region -> region.Color = firstRegionColor)

    override this.ToString() =
        this.Regions
        |> Map.toSeq
        |> Seq.map (fun (_,region) -> sprintf "%O" region)
        |> String.concat "\n"

type PuzzleSolution = {
    // Number of nodes evaluated while looking for a solution. Only useful for
    // evaluating algorithm effectiveness.
    NodesEvaluated: int
    // Number of nodes culled for being duplicate.
    DuplicateSteps: int
    // None indicates no solution was found (timeout?). Otherwise it is a list
    // of RegionID x ColorID pairs.
    Moves: (int * int) list option
}
