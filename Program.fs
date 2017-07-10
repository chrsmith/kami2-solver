module Kami2Solver.Program

open System
open System.Diagnostics
open System.IO
open System.Text.RegularExpressions
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
    | PrintSolution
    | SearchTimeout of int
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | PuzzlesDirectory _ -> "directory containg puzzle images"
            | PuzzleName _       -> "name of a puzzle to solve"
            | PrintRegionInfo    -> "print region information to STDOUT"
            | SaveMarkupImage    -> "save marked up puzzle image"
            | SaveGraphImage     -> "save graph output"
            | PrintSolution      -> "print the puzzle solution to STDOUT"
            | SearchTimeout _    -> "search timeout in seconds"


// Active pattern for matching if an input string contains a substring.
let (|Contains|_|) (p:string) (s:string) =
    if s.Contains(p) then Some()
    else None

 
// Extract puzzle metadata encoded in the file name. Returns:
// (set index, puzzle index, number of moves). User-generated puzzles have
// 0 for the set index.
let tryGetPuzzleDataMetadata input : (int * int * int) option =
    let m1 = Regex.Match(input, "set-(\d+)-puzzle-(\d+)-moves-(\d+).jpg")
    let m2 = Regex.Match(input, "user-generated-(\d+)-moves-(\d+).jpg")
    if m1.Success then
        Some(
            m1.Groups.[1].Value |> int,
            m1.Groups.[2].Value |> int,
            m1.Groups.[3].Value |> int)
    elif m2.Success then
        Some(
            0,
            m2.Groups.[1].Value |> int,
            m2.Groups.[2].Value |> int)
    else
        None


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
    let argPrintSolution = results.TryGetResult(<@ PrintSolution @>) |> Option.isSome

    let timeout = results.GetResult(<@ SearchTimeout @>, 10) |> float

    let puzzleImages = Directory.GetFiles(argPuzzlesDir, "*.jpg")
    for puzzleImagePath in puzzleImages do

        // TODO(chrsmith): The puzzle image has the number of steps (upper bound) required to solve.
        // Hook up some OCR to do this. For now I just put the expected number of moves in the file
        // name, like a scrub.
        let puzzleFileName = Path.GetFileName(puzzleImagePath)
        let puzzleName, moves =
            match tryGetPuzzleDataMetadata puzzleFileName with
            | Some(0, puzzle, moves) -> ((sprintf "user generated #%d (%d)" puzzle moves), moves)
            | Some(set, puzzle, moves) -> ((sprintf "set %d #%d (%d)" set puzzle moves), moves)
            | None -> null, 0

        let shouldSolvePuzzle =
            if puzzleName = null then 
                false
            elif Option.isSome argPuzzleName then
                puzzleFileName.Contains(Option.get argPuzzleName)
            else
                true

        if shouldSolvePuzzle then
            printf "Solving puzzle '%s'...\t" puzzleName

            let puzzle = Analysis.ExtractPuzzle puzzleImagePath argSaveMarkupImage
            if puzzle.Colors.Length <= 1 then
                printfn "ERROR: Unable to determine puzzle colors."
            else
                // Print region information to the console.
                if argPrintRegionInfo then
                    printfn "Puzzle has %d colors and %d regions" puzzle.Colors.Length (Seq.length puzzle.Regions)
                    for region in puzzle.Regions do
                        printfn "%s" <| region.ToDebugString()

                // Print the dot file graph version.
                if argSaveGraphImage then
                    let sourceImageDir = Path.GetDirectoryName(puzzleImagePath)
                    let sourceImageName = Path.GetFileNameWithoutExtension(puzzleImagePath)
                    let graphImagePath = Path.Combine(sourceImageDir, sourceImageName + ".graph.png")
                    Export.RenderAsGraph
                        puzzle.Regions
                        graphImagePath

                // Solve!
                let stopwatch = Stopwatch.StartNew()

                let searchTask, searchResults, cts = Solver.StartBruteForceSearch puzzle moves
                if not <| searchTask.Wait(TimeSpan.FromSeconds(timeout)) then
                    cts.Cancel()

                let timeResult =
                    if cts.Token.IsCancellationRequested then "Timeout"
                    else sprintf "%.2fs" stopwatch.Elapsed.TotalSeconds
                let nodeStats = sprintf "(%d nodes, %d dupes)" searchResults.NodesEvaluated searchResults.DuplicateNodes
                let solutionString =
                    if not searchResults.SolutionFound then "no solution found"
                    else sprintf "SOLVED!"
       
                printfn "%s\t%s\t%s" timeResult nodeStats solutionString
                if argPrintSolution then
                    printfn "%A" searchResults.Moves

    0
