module Kami2Solver.Program

open System
open System.Diagnostics
open System.IO
open System.Threading
open System.Threading.Tasks

open Argu

open Kami2Solver.Types
open Kami2Solver.Analysis
open Kami2Solver.Export
open Kami2Solver.Solver

// TODO(chrsmith): Review and apply F# style guidelines:
// http://fsharp.org/specs/component-design-guidelines/

type CommandLineArguments =
    | PuzzlesDirectory of dir:string
    | PuzzleName of name:string
    | PrintRegionInfo
    | SaveMarkupImage
    | SaveGraphImage
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | PuzzlesDirectory _ -> "directory containg puzzle images"
            | PuzzleName _ -> "name of a puzzle to solve"
            | PrintRegionInfo -> "print region information to STDOUT"
            | SaveMarkupImage -> "save marked up puzzle image"
            | SaveGraphImage -> "save graph output"

// Active pattern for matching if an input string contains a substring.
let (|Contains|_|) (p:string) (s:string) =
    if s.Contains(p) then Some()
    else None


// Timeout in seconds to wait while trying to solve a puzzle.
let kMinuteInSeconds = 60.0
let kTimeoutSeconds = 2.0 * kMinuteInSeconds

[<EntryPoint>]
let main argv =
    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<CommandLineArguments>(programName = "kami2solver", errorHandler = errorHandler)

    let results = parser.ParseCommandLine argv

    let argPuzzlesDir = results.GetResult(<@ PuzzlesDirectory @>, defaultValue = "./puzzles/")
    let argPuzzleName = results.TryGetResult(<@ PuzzleName @>)
    let argPrintRegionInfo = results.TryGetResult(<@ PrintRegionInfo @>) |> Option.isSome
    let argSaveMarkupImage =  results.TryGetResult(<@ SaveMarkupImage @>) |> Option.isSome
    let argSaveGraphImage =  results.TryGetResult(<@ SaveGraphImage @>) |> Option.isSome

    let puzzleImages = Directory.GetFiles(argPuzzlesDir, "*.jpg")
    for puzzleImagePath in puzzleImages do
        let solvePuzzle = match argPuzzleName with
                          | None -> true
                          | Some(name) -> puzzleImagePath.Contains(name)
        if solvePuzzle then
            printf "Solving puzzle [%s]...\t" (Path.GetFileName(puzzleImagePath))

            let puzzle = Analysis.ExtractPuzzle puzzleImagePath argSaveMarkupImage
            // TODO(chrsmith): The puzzle image has the number of steps (upper bound) required to solve.
            // Hook up some OCR to do this. For now I will use a lame lookup.
            let maxDepth = match Path.GetFileName(puzzleImagePath) with
                           | Contains "IMG_1730" -> 15
                           | Contains "IMG_1731" -> 14
                           | Contains "IMG_1732" -> 6
                           | Contains "IMG_1733" -> 10
                           | Contains "IMG_1735" -> 2
                           | Contains "IMG_1740" -> 2
                           | Contains "IMG_1741" -> 10
                           | Contains "IMG_1742" -> 12
                           | Contains "IMG_1743" -> 4
                           | _ -> 5  // Default

            if argPrintRegionInfo then
                printfn "Puzzle has %d colors and %d regions" puzzle.Colors.Length (Seq.length puzzle.Regions)
                for region in puzzle.Regions do
                    printf "Region %d [%d, #%s] [%d triangles]-> " region.ID region.Color region.ColorCode region.Size
                    for adjRegion in region.AdjacentRegions do
                        printf "%d " adjRegion
                    printfn ""

            // Print the dot file graph version.
            if argSaveGraphImage then
                let sourceImageDir = Path.GetDirectoryName(puzzleImagePath)
                let sourceImageName = Path.GetFileNameWithoutExtension(puzzleImagePath)
                let graphImagePath = Path.Combine(sourceImageDir, sourceImageName + ".graph.png")
                Export.RenderAsGraph
                    puzzle.Regions
                    graphImagePath

            // Solve it. Put execution in a task so we can provide a timeout.
            let mutable executionResult = ""
            let stopwatch = Stopwatch.StartNew()

            let cts = new CancellationTokenSource()
            let solution = lazy (Solver.BruteForce puzzle maxDepth cts.Token)

            let solverTask = Task.Run((fun () -> solution.Force()), cts.Token)
            if not <| solverTask.Wait(TimeSpan.FromSeconds(kTimeoutSeconds)) then
                cts.Cancel()

            let solution = solution.Force()  // wart
            let timeResult =
                if cts.Token.IsCancellationRequested then
                    // Take up as much space as TimeSpan.ToString
                    // TODO(chrsmith): Format specifier in printfn?
                    "Timeout          "
                else
                    stopwatch.Elapsed.ToString()

            printfn "%s\tEvaluated %d nodes (%d dupes)" timeResult solution.NodesEvaluated solution.DuplicateSteps
            if Option.isSome solution.Moves then
                printfn "\tSolution %O" <| Option.get solution.Moves

    0
