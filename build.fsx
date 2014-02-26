// --------------------------------------------------------------------------------------
// FAKE build script, see http://fsharp.github.io/FAKE
// --------------------------------------------------------------------------------------

#r @"packages/FAKE/tools/FakeLib.dll"
open Fake 
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open System

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__


// PREPARE

Target "Start" DoNothing
Target "Clean" (fun _ -> CleanDirs ["out"; "obj"; "temp"])
Target "RestorePackages" RestorePackages
Target "AssemblyInfo" DoNothing
Target "Prepare" DoNothing

"Start"
  =?> ("Clean", not (hasBuildParam "incremental"))
  ==> "RestorePackages"
  ==> "AssemblyInfo"
  ==> "Prepare"


// BUILD

let build subject = MSBuildRelease "" (if hasBuildParam "incremental" then "Build" else "Rebuild") subject |> ignore

Target "BuildMain" (fun _ -> build !! "MathNet.Numerics.sln")
Target "BuildNet35" (fun _ -> build !! "MathNet.Numerics.Net35Only.sln")
Target "BuildFull" (fun _ -> build !! "MathNet.Numerics.Portable.sln")
Target "Build" DoNothing

"Prepare"
  =?> ("BuildNet35", hasBuildParam "net35")
  =?> ("BuildFull", hasBuildParam "full")
  =?> ("BuildMain", not (hasBuildParam "full" || hasBuildParam "net35"))
  ==> "Build"


// TEST

Target "Test" (fun _ ->
    !! "out/test/*/*UnitTests*.dll"
    |> NUnit (fun p ->
        { p with
            DisableShadowCopy = true
            TimeOut = TimeSpan.FromMinutes 30.
            OutputFile = "TestResults.xml" })
)

"Build" ==> "Test"


// DOCUMENTATION

Target "CleanDocs" (fun _ -> CleanDirs ["out" @@ "docs"])

Target "Docs" (fun _ ->
    executeFSIWithArgs "docs/tools" "build-docs.fsx" ["--define:RELEASE"] [] |> ignore
)

Target "DocsDev" (fun _ ->
    executeFSIWithArgs "docs/tools" "build-docs.fsx" [] [] |> ignore
)

"Build"  ==> "Docs"

"Start"
  =?> ("CleanDocs", not (hasBuildParam "incremental"))
  ==> "DocsDev"


// NUGET

Target "NuGet" (fun _ ->
    // TODO: Replace with FAKE's internal NuGet support and drop the MSBuild project
    !! "build/NuGet/nuget.proj" |> MSBuild "" "BuildPackages" [] |> ignore
)

"BuildFull" ==> "NuGet"


// RUN

Target "Release" DoNothing
"Test" ==> "Release"
"Docs" ==> "Release"
"NuGet" ==> "Release"

Target "All" DoNothing
"Build" ==> "Test" ==> "All"

RunTargetOrDefault "All"
