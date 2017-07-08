module Kami2Solver.Export

open System
open System.Diagnostics
open System.IO

open SkiaSharp

open Kami2Solver.Types


// Size of annotations in the debug image.
let kAnnotationSize = 16.0f


// Kami2 puzzle image with annotations to aid in debugging.
type AnalysisDebugImage(originalImage : SKBitmap) =
    let surface = SKSurface.Create(originalImage.Width,
                                   originalImage.Height,
                                   SKImageInfo.PlatformColorType,
                                   SKAlphaType.Premul)
    let canvas = surface.Canvas
    let paint = new SKPaint(TextSize = 40.0f,
                            IsAntialias = true,
                            Color = SKColor(0uy, 0uy, 0uy),
                            StrokeWidth = 2.0f)

    do canvas.DrawBitmap(originalImage, 0.0f, 0.0f, paint)

    member this.DrawLine(x0, y0, x1, y1, color) =
        paint.Color <- color
        canvas.DrawLine(x0, y0, x1, y1, paint)

    member this.AddCircle(x, y, color) =
        paint.IsStroke <- false
        paint.Color <- color
        canvas.DrawCircle(
            x, y, kAnnotationSize,
            paint)

    member this.AddCircleOutline(x, y, color) =
        paint.IsStroke <- true
        paint.Color <- color
        canvas.DrawCircle(
            x, y, kAnnotationSize,
            paint)

    member this.AddText(text : string, x : float32, y : float32, color) =
        // Shift a little bit so it doesn't overlap.
        let x = x - 16.0f
        let y = y + 16.0f;

        paint.Color <- color
        paint.IsStroke <- false
        canvas.DrawText(text, x, y, paint)

        paint.Color <- SKColor(255uy, 255uy, 255uy)
        paint.IsStroke <- true
        canvas.DrawText(text, x, y, paint)

    // Save the image to disk in the PNG format.
    member this.Save(filePath) =
        let finalImage = surface.Snapshot()
        use pngEncodedFile = finalImage.Encode();
        File.WriteAllBytes(filePath, pngEncodedFile.ToArray())

    // Clean up Skia objects.
    interface IDisposable with
        member __.Dispose() =
            paint.Dispose()
            canvas.Dispose()
            surface.Dispose()

// Emit the dot file representation for a puzzle's regions/nodes.
let emitRegionLabels (regions : seq<Region>) =
    regions
    |> Seq.map (fun region ->
        sprintf "    r_%d [style=\"filled\", fillcolor=\"%s\"]"
                region.ID
                region.ColorCode)

// Emit the dot file representation for a region's associations.
let emitRegionAssociations (region : Region) =
    region.AdjacentRegions
    |> Seq.map (sprintf "r_%d")
    |> Seq.map (sprintf "r_%d -- %s" region.ID)


// Convert a Kami2 puzzle into a string dot file, to be rendered.
let ConvertToGraph (regions : seq<Region>) =
    seq {
        yield "strict graph kami_puzzle {"
        // Emit the nodes. Give each a number (since puzzles with more than
        // 26 regions are common) and assign the source color.
        yield "    // labels"
        yield! emitRegionLabels regions
        yield "    // edges"
        yield! regions
               |> Seq.map emitRegionAssociations
               |> Seq.concat
               |> Seq.map (sprintf "    %s")
            
        yield "}"
    }

// Renders the regions as a PNG file.
let RenderAsGraph (regions : seq<Region>) outputFilePath =
    let dotFilePath = Path.GetTempFileName()
    let dotFileLines = ConvertToGraph regions
    File.WriteAllLines(dotFilePath, dotFileLines)

    // Render, assuming `dot` is on the current path.
    let args = sprintf "-Tpng %s -o %s"
                        dotFilePath
                        outputFilePath
    Process.Start("dot", args) |> ignore