namespace Kami2Solver.Types

open System.Collections.Generic

type Color = { Red: byte; Green: byte; Blue: byte }

// Raw puzzle extracted from a screenshot.
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


// Kami2 puzzle represented as a graph.
type ImmutableRegion = {
    ID: int
    Color: int
    Position: int * int
    ColorCode: string
    Size: int
    AdjacentRegions: Set<int>
} with
    override this.ToString() =
        sprintf
            "[%d][c%d] -> %A"
            this.ID this.Color this.AdjacentRegions


type Region = {
    ID: int
    Color: int
    // col, row position of a triangle in the region.
    Position: int * int
    // Hex value of the RGB value of the color index.
    ColorCode: string
    // Number of triangles in the region.
    mutable Size: int
    // IDs of adjacent regions
    AdjacentRegions: HashSet<int>
} with
    member this.AddAdjacentRegion(adjacentRegion : Region) =
        if adjacentRegion.ID <> this.ID then
            this.AdjacentRegions.Add(adjacentRegion.ID) |> ignore
            adjacentRegion.AdjacentRegions.Add(this.ID) |> ignore

    member this.Convert() =
        {
            ImmutableRegion.ID = this.ID
            ImmutableRegion.Color = this.Color
            ImmutableRegion.Position = this.Position
            ImmutableRegion.ColorCode = this.ColorCode
            ImmutableRegion.Size = this.Size
            ImmutableRegion.AdjacentRegions = Set.ofSeq this.AdjacentRegions
        }

    override this.ToString() =
        sprintf
            "[%d][c%d] -> %A"
            this.ID this.Color (Set.ofSeq this.AdjacentRegions)

    

         


// Kami2 puzzle in the process of being solved.
// Using the immutable collection types so they can be reused
// while doing tree search for a puzzle solution.
// https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-collection-types
type Kami2Puzzle = {
    NumColors: int
    Regions: List<Region>
}


type Kami2PuzzleStep = {
    Regions: Map<int, ImmutableRegion>
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
