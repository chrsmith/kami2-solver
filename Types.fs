namespace Kami2Solver.Types

open System.Collections.Generic
open System.Threading

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
    member this.ToDebugString() =
        let region = this
        seq {
            yield sprintf "Region %d [%d, #%s] [%d triangles]-> " region.ID region.Color region.ColorCode region.Size
            yield! region.AdjacentRegions |> Seq.map (sprintf "%d") }
        |> String.concat " "


    override this.ToString() =
        sprintf
            "[%d][c%d] -> %A"
            this.ID this.Color (Set.ofSeq this.AdjacentRegions)

    member this.JankHash() =
        // First 100 prime numbers. Used in GetHashCode implementations. Also, FML.
        let primes = [|   2; 3; 5; 7; 11; 13; 17; 19; 23; 29; 31; 37; 41; 43; 47; 53; 59; 61; 67; 71;
                         73; 79; 83; 89; 97;  101; 103; 107; 109; 113; 127; 131; 137; 139; 149; 151;
                        157; 163; 167; 173; 179; 181; 191; 193; 197; 199; 211; 223; 227; 229; 233;
                        239; 241; 251; 257; 263; 269; 271; 277; 281; 283; 293; 307; 311; 313; 317;
                        331; 337; 347; 349; 353; 359; 367; 373; 379; 383; 389; 397; 401; 409; 419;
                        421; 431; 433; 439; 443; 449; 457; 461; 463; 467; 479; 487; 491; 499; 503;
                        509; 521; 523; 541|]
        let (x, y) = this.Position
        let regionsHashCode =
            this.AdjacentRegions
            |> Seq.mapi (fun i rid -> rid * primes.[i])
            |> Seq.reduce (fun hc1 hc2 -> hc1 ^^^ hc2)

        ((0xAA + this.ID * 7 + this.Color * 5 + this.Size * 3) <<< this.Color)
        |> (fun hc -> hc ^^^ x * 463 + y * 499)
        // |> (fun hc -> hc ^^^ this.ColorCode.GetHashCode())  // Not stable between builds.
        |> (fun hc -> hc ^^^ regionsHashCode)

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

// Simplified data structure to represent a Kami2 puzzle. Uses an immutable Map
// to avoid GC pressure.
type Kami2PuzzleStep = {
    // Mapping from RegionID to Region. (More useful than a flat Seq<Region>.)
    Regions: Map<int, Region>
} with
    member this.GetRegions() =
        this.Regions
        |> Map.toSeq
        |> Seq.map snd

    member this.IsSolved =
        let firstRegionColor =
            this.GetRegions()
            |> Seq.head
            |> (fun region -> region.Color)

        this.Regions
        |> Map.forall (fun _ region -> region.Color = firstRegionColor)

    override this.ToString() =
        this.Regions
        |> Map.toSeq
        |> Seq.map (fun (_,region) -> sprintf "%O" region)
        |> String.concat "\n"

    // Like GetHashCode, but wihout being implemented correctly.
    // TODO(chrsmith): Figure out what F#'s defaults are for Set, Map, and
    // record types... Why is this needed?
    member this.JankHash() =
        (*
        this.GetRegions()
        |> Seq.map (fun r -> r.JankHash())
        |> Seq.reduce (fun hash acc -> hash ^^^ acc)
        *)
        this.GetRegions()
        |> Seq.map(fun r -> r.ToDebugString())
        |> Seq.map (sprintf "%s")
        |> String.concat "\n"
    
    member this.DebugPrint() =
        this.GetRegions()
        |> Seq.map(fun r -> r.ToDebugString())
        |> Seq.iter (printfn "%s")


// Region ID and Color ID to paint it as.
type PuzzleMove = int * int

type SearchResults = {
    // Number of nodes evaluated while looking for a solution. Only useful for
    // evaluating algorithm effectiveness.
    mutable NodesEvaluated: int
    // Number of nodes culled for being duplicate.
    mutable DuplicateNodes: int
    // Hash set of known nodes. Used to cull duplicate puzzle steps.
    mutable KnownNodes: HashSet<string>
    CancellationToken: CancellationToken
    // None indicates no solution was found (timeout?). Otherwise it is the list
    // of move to perform.
    mutable Moves: PuzzleMove list
} with
   member this.SolutionFound = (List.length this.Moves) > 0
   member this.IncrementNodesEvaluated() = this.NodesEvaluated <- this.NodesEvaluated + 1
   member this.IncrementDupNodes() = this.DuplicateNodes <- this.DuplicateNodes + 1
