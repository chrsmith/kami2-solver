open System
open System.IO
open System.Collections.Generic

open SkiaSharp

// F# black magic for invoking implicit conversions.
// https://stackoverflow.com/questions/10719770/is-there-anyway-to-use-c-sharp-implicit-operators-from-f
(*
// Use a SKRectI where an SKRect is required. In C# this would work via implicit
// conversion. In F# you need to cast this spell.
canvas.DrawRect(
        !> (new SKRectI(32, 32, 32 + 8, 32 + 8)),
        paint
    )
*)
let inline (!>) (x:^a) : ^b = ((^a or ^b) : (static member op_Implicit : ^a -> ^b) x) 


let kBlack = new SKColor(0uy, 0uy, 0uy, 255uy)
let kMagenta = new SKColor(200uy, 0uy, 200uy, 255uy)

// Computes the dot product of the two colors as if they were
// normalized vectors.
let dotProduct colorA colorB =
    let colorToNormVec (c : SKColor) =
        let valueArray =
            [| c.Red; c.Green; c.Blue; c.Alpha |]
            |> Array.map float
        let w, x, y, z = (valueArray.[0], valueArray.[1], valueArray.[2], valueArray.[3])
        let length = Math.Sqrt(w * w + x * x + y * y + z * z)
        Array.map (fun x -> (float x) / length) valueArray
    let vecA = colorToNormVec colorA
    let vecB = colorToNormVec colorB    
    Array.fold2 (fun acc x y -> acc + x * y) 0.0 vecA vecB

// Returns the average SKPixel value for those near the given point.
let getColorAverage (image : SKBitmap) x y =
    let x, y = (int x), (int y)
    let colorTotals = [| 0; 0; 0; 0 |]
    let addAtIdx i x = colorTotals.[i] <- colorTotals.[i] + (int x)
    let mutable samples = 0
    let kRange = 5
    for xOffset in [-kRange..kRange] do
        for yOffset in [-kRange..kRange] do
            let color = image.GetPixel(x + xOffset, y + yOffset)
            samples <- samples + 1
            addAtIdx 0 color.Red
            addAtIdx 1 color.Green
            addAtIdx 2 color.Blue
            addAtIdx 3 color.Alpha
    let averages = colorTotals |> Array.map (fun total -> total / samples) |> Array.map byte
    new SKColor(averages.[0], averages.[1], averages.[2], averages.[3])


// Raw puzzle extracted from a screenshot.
type RawKami2Puzzle = {
    // Number of colors used in the puzzle.
    NumColors: int
    // Index of the color of each individual triagle. See other comments for
    // translating coordinates to triangles, and adjacency rules.
    Triangles: int[,]
}


// Analyze a screen shot of a Kami2 puzzle and convert it into a RawKami2Puzzle
// object. Also creates an annotated copy of the image for debugging purposes.
let AnalyzePuzzleImage filePath =
    // Load the puzzle image.
    let filePath = "./test/puzzle.jpg"
    use fileStream = File.OpenRead(filePath)
    use skiaStream = new SKManagedStream(fileStream)
    use bitmap = SKBitmap.Decode(skiaStream)

    // Values in pixels for the dimensions of things. These are hard-coded
    // to the images I'm generating on my phone, which might vary between
    // iPhone models. (Sourced from 1242 × 2208.)
    let kGameFooter = 206.0f  // Size in px of the game's footer
    let kGridWidth = bitmap.Width |> float32
    let kGridHeight = (bitmap.Height |> float32) - kGameFooter

    // Dimensions of the color swatches in the bottom right.
    let kColorPaletWidth = 745.0f
    let kColorPaletXOffset = kGridWidth - kColorPaletWidth

    // Width/height of the triangle grid.
    let kColWidth = kGridWidth / 10.0f
    let kRowHeight = kGridHeight / 28.0f

    // Size of annotations in the debug image.
    let kAnnotationSize = 22.0f

    // Surface for the debugging annotations.
    use surface = SKSurface.Create(bitmap.Width,
                                   bitmap.Height,
                                   SKImageInfo.PlatformColorType,
                                   SKAlphaType.Premul)
    let canvas = surface.Canvas

    // Draw the original puzzle image.
    canvas.DrawColor(SKColors.White)
    use paint = new SKPaint(TextSize = 96.0f,
                            IsAntialias = true,
                            Color = kBlack,
                            StrokeWidth = 5.0f)
    canvas.DrawBitmap(bitmap, 0.0f, 0.0f, paint)

    paint.Color <- kMagenta
    canvas.DrawText("Kami2 Solver", 8.0f, kGridHeight + 128.0f, paint)

    // Add a colored dot with outline on the annotated image.
    let addAnnotation x y color =
        paint.IsStroke <- true
        paint.Color <- kBlack
        canvas.DrawCircle(
            x, y, kAnnotationSize,
            paint
        )

        paint.IsStroke <- false
        paint.Color <- color
        canvas.DrawCircle(
            x, y, kAnnotationSize,
            paint
        )

    // Draw guiding lines for rows and columns.
    paint.Color <- kBlack
    for col = 0 to 9 do
        let colf = col |> float32
        let colStartX = colf * kColWidth
        canvas.DrawLine(colStartX, 0.0f,
                        colStartX, kGridHeight,
                        paint)
    for row = 0 to 28 do
        let rowf = row |> float32
        let rowStartY = rowf * kRowHeight
        canvas.DrawLine(0.0f, rowStartY,
                        kGridWidth, rowStartY,
                        paint)

    // Get the puzzle's colors and mark them on the analyzed image.
    let getPuzzleColors() =
        // TODO(chrsmith): Do some better logic here. Maybe start with 10
        // colors and whittle them down till all are unique?
        let puzzleColorsUsed = 5
        let colorSwatchWidth = kColorPaletWidth / (float32 puzzleColorsUsed)

        // Get the average SKColor representing the ith puzzle color.
        let getPuzzleColor i =
            let x = kColorPaletXOffset + (float32 i) * colorSwatchWidth + colorSwatchWidth / 2.0f
            let y = kGridHeight + kGameFooter / 2.0f
            let color = getColorAverage bitmap x y
            addAnnotation x y color
            color

        Array.init puzzleColorsUsed getPuzzleColor

    let puzzleColors : SKColor[] = getPuzzleColors()

    // Get the inferred index of the color correspoding to the triangle at the
    // given column and row.
    let getTriangleColor col row =
        // First we map the col/row to a coordinate on the input image.
        // We shift along the X axis to account for the trangular grid.
        let colShift = if (col + row) % 2 = 0 then -kColWidth / 5.0f else kColWidth / 5.0f
        // Shift the first/last row to avoid reading pixels off the grid.
        let rowShift =
            if row = 0 then     6.0f
            elif row = 28 then -6.0f
            else                0.0f

        let x = (col |> float32) * kColWidth + kColWidth / 2.0f + colShift
        let y = (row |> float32) * kRowHeight + rowShift
        let triangleColor = getColorAverage bitmap x y
        addAnnotation x y triangleColor

        puzzleColors
        |> Seq.mapi (fun idx puzzleColor -> (idx, dotProduct triangleColor puzzleColor))
        |> Seq.maxBy snd
        |> fst

    let puzzleInfo = {
        NumColors = puzzleColors.Length
        Triangles = Array2D.init 10 29 getTriangleColor
    }

    // Save the image _after_ puzzleInfo constructed, since getTriangleColor
    // has the side effect of annotating the debug image.
    let finalImage = surface.Snapshot()
    use pngEncodedFile = finalImage.Encode();
    File.WriteAllBytes("demo.png", pngEncodedFile.ToArray())

    puzzleInfo


[<EntryPoint>]
let main argv =

    let filePath = "./test/puzzle.jpg"
    let rawPuzzle = AnalyzePuzzleImage filePath

    printfn "Puzzle '%s'" filePath
    printfn "Using %d colors" rawPuzzle.NumColors
    for row = 0 to 28 do
        printf "\t> "
        for col = 0 to 9 do
            printf "%d\t" rawPuzzle.Triangles.[col, row]
        printfn ""

    printfn "Done"

    0