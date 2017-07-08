module Kami2Solver.Solver

open System.Collections.Generic
open System.Threading
open System.Threading.Tasks

open Kami2Solver.Types


// Color a region in the given puzzle, returning the new puzzle state.
// This is done by essentially "deleting" any existing adjacent regions
// that have the newColor as a color. And merge them into regionId.
// Returns a new Kami2PuzzleStep.
let colorRegion puzzle regionId newColor =
    let regionToColor = Map.find regionId puzzle.Regions

    // First, we create a new region and adjacent neighbor list.

    // Partition that region's neighbors into those that match the new color
    // and those that don't.
    let colorMatchingNeighbors, nonColorMatchingNeighbors =
        regionToColor.AdjacentRegions
        |> Set.partition (fun regionId ->
            let region = puzzle.Regions.[regionId]
            (region.Color = newColor))

    // The non-color matching neighbors are just fine as-is.
    //
    // As for the color matching neighbors, we need to union all of their adjacent
    // neighbors, as they are now neighbors of the larger, newly colored region.
    let newSetOfNeighbors =
        colorMatchingNeighbors
        |> Set.toSeq
        |> Seq.map (fun regionId -> puzzle.Regions.[regionId])
        |> Seq.map (fun region -> region.AdjacentRegions)
        |> Set.unionMany

    // Size of the new, merged region.
    let colorMatchingNeighborSize =
        colorMatchingNeighbors
        |> Seq.map (fun neighborID -> puzzle.Regions.[neighborID].Size)
        |> Seq.sum

    // Now we replace the target region in the puzzle.
    let newlyColoredRegion = {
        regionToColor with 
            Color = newColor
            AdjacentRegions =
                Set.union newSetOfNeighbors nonColorMatchingNeighbors
                // The region being colored is a neighbor of colorMatchingNeighbors,
                // but self-refential regions gum up the gears.
                |> Set.remove regionId
            Size = colorMatchingNeighborSize + puzzle.Regions.[regionId].Size
    }

    // Second, we remove all references to the subsumed regions. This is done
    // by updating AdjacentRegions sets for all regions. But as an optimization,
    // we only consider the regions for newSetOfNeighbors, since those are the
    // only regions that would have a reference to the subsumed regions.
    //
    // So we generate the puzzle by taking all regions, and partitioning to
    // those that are adjacent/are the newly colored region. Or are not.
    let adjacentToNewlyColoredRegion, notAdjacent =
        puzzle.Regions
        |> Map.toSeq |> Seq.map snd
        // Filter out the original node.
        |> Seq.filter (fun region -> region.ID <> regionId)
        // Filter out colorMatchingNeighbors (nodes being subsumed by coloring).
        |> Seq.filter (fun region -> not <| Set.contains region.ID colorMatchingNeighbors)
        // No seq.partition :'( Split on if they are in the newlyColored object's
        // neighbors.
        |> Seq.toArray
        |> Array.partition (fun region ->
            Set.contains region.ID newlyColoredRegion.AdjacentRegions)

    // Adjacent to the newly colored region need their adjancent IDs updated,
    // since the previously adjacent node might no longer exist. (Replace ID with
    // that of regionId.)
    let adjacentToNewlyColoredRegion =
        adjacentToNewlyColoredRegion
        |> Seq.map (fun region ->
            { region with
                AdjacentRegions =
                    Set.difference region.AdjacentRegions colorMatchingNeighbors
                    |> Set.add regionId
            })

    {
        Regions =
            seq {
                yield newlyColoredRegion
                yield! adjacentToNewlyColoredRegion
                yield! notAdjacent
            }
            |> Seq.map (fun region -> region.ID, region)
            |> Map.ofSeq
    }

// Enumerates all valid moves from a given puzzle step.
let enumerateAllMoves (puzzleStep : Kami2PuzzleStep) : seq<PuzzleMove> =
    let remainingColors = new HashSet<int>()

    do puzzleStep.GetRegions()
       |> Seq.iter (fun region -> remainingColors.Add(region.Color) |> ignore)

    let possibleMoves = 
        puzzleStep.GetRegions()
        |> Seq.map (fun region ->
            let neighborColors = 
                region.AdjacentRegions
                |> Seq.map (fun neighborID -> puzzleStep.Regions.[neighborID])
                |> Seq.map (fun neighborRegion -> neighborRegion.Color)
                |> (fun neighborColors -> new HashSet<int>(neighborColors))
            
            assert (not <| neighborColors.Contains(region.Color))
   
            // Valid moves are from painting this region every remaining color.
            remainingColors
            // Don't paint the region its current color. (No op.)
            |> Seq.filter (fun colorId -> colorId <> region.Color)
            // Only consider colors which would merge with a neighbor.
            |> Seq.filter (fun colorId -> neighborColors.Contains(colorId))
            |> Seq.map (fun colorId -> (region.ID, colorId)))
        |> Seq.concat
        |> Seq.sortBy (fun (region, color) -> region * 100 + color)
    possibleMoves


// Heuristic to evaluate a potential move. A value of <= 0 means the resulting
// puzzle is not solveable, ando therefore should not be considered. The higher
// the value the more promising the move is.
let evaluateMove (puzzleStep : Kami2PuzzleStep) (regionToColor, newColor) movesLeft =
    // If there are more colors remaining than moves, you can't paint them all
    // in time.
    let remainingColors = new HashSet<int>()
    do puzzleStep.GetRegions()
       |> Seq.iter (fun region -> remainingColors.Add(region.Color) |> ignore)
    let possibleToBeSolved = remainingColors.Count <= movesLeft + 1

    let regionBeingColored = puzzleStep.Regions.[regionToColor]
    // Assert we have at least one neighbor of the region being colored.
    // WARNING: Some puzzles have singleton regions. So this isn't universally true.
    assert ((Set.count regionBeingColored.AdjacentRegions) > 0)

    let (neighborsColored, totalTrianglesColored) =
        regionBeingColored.AdjacentRegions
        |> Set.toSeq
        |> Seq.map (fun regionId -> puzzleStep.Regions.[regionId])
        |> Seq.fold (fun (neighborsColored, totalTrianglesColored) neighbor ->
            if neighbor.Color = newColor then
                (neighborsColored + 1, totalTrianglesColored + neighbor.Size)
            else
                (neighborsColored, totalTrianglesColored)) (0, 0)

    if possibleToBeSolved && neighborsColored > 0 then
        assert (neighborsColored > 0)
        assert (totalTrianglesColored > 0)
        20 * neighborsColored + totalTrianglesColored
    else
        0

type StepResult = Cancelled | Culled | Duplicate | Solved of PuzzleMove list | OutOfMoves

let rec bruteForceStep (puzzleStep : Kami2PuzzleStep) movesList movesRemaining (state : SearchResults) =
    let puzzleStepHashCode = puzzleStep.JankHash()
    state.IncrementNodesEvaluated()

    // See if we can cull the search at this point.
    // User cancelled the search task?
    if state.CancellationToken.IsCancellationRequested then
        Cancelled
    // Have we already seen this puzzle step before?
    elif state.KnownNodes.Contains(puzzleStepHashCode) then
        state.IncrementDupNodes()
        Duplicate
    // Is the puzzle actually solved?
    elif puzzleStep.IsSolved then
        // We built up the move list "backwards", so reverse to make it right.
        Solved(List.rev movesList)
    // Ran out of moves? (Hopefully we never hit this, as we cull unsolveable
    // puzzles earlier in the search tree.)
    elif movesRemaining <= 0 then
        OutOfMoves
    // Otherwise, try making a move and recursing.
    else
        // Since we are in the process of evaluating this node, add it to the
        // known nodes list. So a peer / parent doesn't reevaluate.
        state.KnownNodes.Add(puzzleStepHashCode) |> ignore

        enumerateAllMoves puzzleStep
        // Evaluate each of these potential moves.
        |> Seq.map (fun (regionToColor, newColor) ->
            let evaluation = evaluateMove puzzleStep (regionToColor, newColor) movesRemaining
            (regionToColor, newColor, evaluation))
        // Filter out the ones that are unsolveable.
        |> Seq.filter (fun (_,_,eval) -> eval > 0)
        // Sort best to worst.
        |> Seq.sortByDescending (fun (_,_,eval) -> eval)
        // Recurse.
        |> Seq.map (fun (regionToColor, newColor, _) ->
            let updatedPuzzle = colorRegion puzzleStep regionToColor newColor
            assert (updatedPuzzle.JankHash() <> puzzleStepHashCode)

            let result = bruteForceStep updatedPuzzle ((regionToColor, newColor) :: movesList) (movesRemaining - 1) state
            result)
        // For each of these potential moves, did we find a solution?
        |> Seq.tryFind (function Solved(moves) -> true | _ -> false)
        |> (function Some(sln) -> sln | None -> Culled)


// Returns the list of (regionID, colorID) moves to make if a solution is found. Mutable SearchResults object is
// built up during execution. (Code smell. Perhaps returned from a call to beginSearch?
let StartBruteForceSearch (kami2Puzzle : Kami2Puzzle) moves =
    let cts = new CancellationTokenSource()

    let startingPuzzle : Kami2PuzzleStep = {
        Regions = kami2Puzzle.Regions
                  |> Seq.map (fun region -> region.ID, region)
                  |> Map.ofSeq
    }

    // Results object that will be fleshed out while the search is being conducted.
    // Is this going to break if converted to be multi-threaded? Absolutely.
    let searchResults = {
        NodesEvaluated = 0
        DuplicateNodes = 0
        KnownNodes = new HashSet<string>()
        CancellationToken = cts.Token
        Moves = []
    }

    let searchFunction() = 
        let result = bruteForceStep startingPuzzle [] (moves + 1) searchResults
        match result with
        | Solved(moveList) -> searchResults.Moves <- moveList
        | _ -> ()

    let searchTask = Task.Run(searchFunction, cts.Token)
    searchTask, searchResults, cts