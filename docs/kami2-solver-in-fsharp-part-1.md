# Kami2 Solver written in F# - Part 1

Kami2

Part 1 - Puzzle representation in JSON and automated extraction.

Problem: How will we represent the puzzle for our solver.
- Ideally JSON. Just because it is perhaps the easiest format to deal with
  these days. (Though not idea for .NET, IMHO)
- How can we extract the puzzle metadata from an image, since manually
  encoding things is lame.

Let's start with the hard part, image extraction.

- We know the colors from the bottom right. It might be a variable number,
  but it shouldn't be too difficult.
- As for extracting the data, we need to understand how to represent the
  triangular grid. (https://en.wikipedia.org/wiki/Synergetics_coordinates?)
  - Assuming this is a non-problem, let's just start writing code.
    Get to first problem: How to load the image?

Problem: Loading images in .NET Core. Evidnetly not great. Since the .NET Desktop
requires Windows-specific OS capabilities (GDI+). This blog post
https://blogs.msdn.microsoft.com/dotnet/2017/01/19/net-core-image-processing/
captures some of the options. But having more than one way to do things is
never ideal.

https://github.com/dotnet/corefx/issues/20325

While it is unclear how things will land, it is pretty clear that the goal
is to have parity with (most?) of System.Drawing. So today the route to take
should be 

But for today, we'll use 
CoreCompat.System.Drawing
https://www.nuget.org/packages/CoreCompat.System.Drawing/1.0.0-beta006

dotnet new console --language fsharp
dotnet restore
dotnet run

error: Unable to resolve 'CoreCompat.System.Drawing' for '.NETCoreApp,Version=v1.1'.
error: Package 'CoreCompat.System.Drawing' is incompatible with 'all' frameworks in project

Bullshit... https://github.com/NuGet/Home/issues/4699
Need to mention explicit version, since it is pre-release.

dotnet add package CoreCompat.System.Drawing -v 1.0.0-beta006
dotnet add package runtime.osx.10.10-x64.CoreCompat.System.Drawing

````
Unhandled Exception: System.TypeInitializationException: The type initializer for 'System.Drawing.GDIPlus' threw an exception. ---> System.DllNotFoundException: Unable to load DLL 'gdiplus': The specified module could not be found.
 (Exception from HRESULT: 0x8007007E)
   at System.Drawing.GDIPlus.GdiplusStartup(UInt64& token, GdiplusStartupInput& input, GdiplusStartupOutput& output)
   at System.Drawing.GDIPlus..cctor()
   --- End of inner exception stack trace ---
   at System.Drawing.GDIPlus.GdipLoadImageFromFile(String filename, IntPtr& image)
   at System.Drawing.Image.FromFile(String filename, Boolean useEmbeddedColorManagement)
   at System.Drawing.Image.FromFile(String filename)
   at Program.main(String[] argv) in /Users/chrsmith/src/github.com/chrsmith/kami2-solver/src/PuzzleLoader/PuzzleLoader/Program.fs:line 9
````

Tried updating the OS X-specific version:
dotnet add package runtime.osx.10.10-x64.CoreCompat.System.Drawing -v 1.0.1-beta004

Still fails for the same reason. I'm not willing to install the MDK, etc.

So trying http://imagesharp.net ImageSharp, which sounds nice as an all .NET option.
But since it is in Alpha, it isn't being published on NuGet for some reason. (Even
though the `-Pre` prerelease tag is for that purpose.)

So adding SkiaSharp. I'd like to avoid Google-technologies. But /shrug.

dotnet add package SkiaSharp

API documentation is nice
https://developer.xamarin.com/api/namespace/SkiaSharp/
https://developer.xamarin.com/guides/cross-platform/drawing/introduction/

F# bugs/suggestions:
FS0001: This expression was expected to have type    'byte'    but here has type    'int'
Fixed by
paint.Color = new SKColor(0x42uy, 0x81uy, 0xA4uy)

But for integer literals, why not automatically convert them to other types if no suffix
is specified? e.g. 1 can be treated as 1L, 1ul, 1uy, etc.

## Next steps

Now that we have the puzzle analyzed and in a graph representation, the next step is to
actually write a solver. You could call that AI artifical intelligence, but without all
the big data learning marketing-speak.