using Nexus.Core;
using Nexus.Storage;
using Nexus.Hardware;
using System.Text.Json;
using Xunit;

namespace Nexus.Tests;

public sealed class MouseLabTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"nexus-mouselab-tests-"+Guid.NewGuid().ToString("N"));
    private string Database=>Path.Combine(root,"nexus.db");
    public void Dispose(){TestSqliteCleanup.ReleasePools(root);if(Directory.Exists(root))Directory.Delete(root,true);}
    [Fact]public void SensitivityMathPreservesEdpiAndRequiresKnownAngularCoefficient()
    {Assert.Equal(800,MouseLabMath.Edpi(800,1));Assert.Equal(.5,MouseLabMath.ConvertDpi(800,1,1600));Assert.InRange(MouseLabMath.Cm360(800,1,.022),51.95454,51.95455);Assert.Equal(40,MouseLabMath.Calibrate(20,180));}
    [Theory][InlineData(double.NaN,1)][InlineData(800,double.PositiveInfinity)][InlineData(0,1)][InlineData(800,-1)]
    public void CalculatorsRejectInvalidValues(double dpi,double sensitivity)=>Assert.Throws<ArgumentException>(()=>MouseLabMath.Edpi(dpi,sensitivity));
    [Fact]public void CalibrationsRejectZeroAndUnknownScales()
    {Assert.Throws<ArgumentException>(()=>MouseLabMath.Cm360(800,1,0));Assert.Throws<ArgumentException>(()=>MouseLabMath.Calibrate(20,0));}
    [Fact]public void EmptyAndInsufficientCapturesDoNotFabricateRateOrScore()
    {var empty=MouseAnalyzer.Analyze([]);Assert.Null(empty.EventsPerSecond);Assert.Null(empty.DiagnosticScore);Assert.Empty(empty.IntervalGraph);Assert.Null(MouseAnalyzer.Analyze([new(0,1,0,0,0)]).EventsPerSecond);}
    [Fact]public void KnownEventSeriesProducesDeliveryMetricsAndBoundedGraph()
    {var a=MouseAnalyzer.Analyze(Enumerable.Range(0,2001).Select(i=>new MouseSample(i,1,0,0,0)).ToArray());Assert.Equal(1000,a.EventsPerSecond);Assert.Equal(1,a.MedianIntervalMs);Assert.Equal(0,a.IntervalDeviationMs);Assert.Equal(100,a.DiagnosticScore);Assert.InRange(a.IntervalGraph.Length,1,240);}
    [Fact]public void AbsoluteAndInvalidCapturesCannotProduceRelativeHardwareClaims()
    {var a=MouseAnalyzer.Analyze([new(0,10,10,0,0,true),new(1,20,20,1,120,true)]);Assert.Equal(0,a.Movements);Assert.Equal(2,a.AbsoluteEvents);Assert.Null(a.EventsPerSecond);Assert.Null(a.DiagnosticScore);Assert.Equal(1,a.Clicks);Assert.Equal(1,a.WheelEvents);Assert.Throws<ArgumentException>(()=>MouseAnalyzer.Analyze([new(2,1,0,0,0),new(1,1,0,0,0)]));}
    [Fact]public void ClickPairsAreObservationsAndCaptureLossSuppressesScore()
    {var samples=Enumerable.Range(0,2001).Select(i=>new MouseSample(i,1,0,(ushort)(i is 10 or 20?1:0),0)).ToArray();var a=MouseAnalyzer.Analyze(samples,1);Assert.Equal(1,a.CloseClickPairs);Assert.Equal(10,a.MeanClickIntervalMs);Assert.Null(a.DiagnosticScore);}
    [Fact]public void ButtonTesterCountsPressTransitionsAndNotReleases()
    {var a=MouseAnalyzer.Analyze([new(0,0,0,1|4|16|64|256,0),new(1,0,0,2|8|32|128|512,0),new(2,0,0,1,0)]);Assert.Equal(new[]{2,1,1,1,1},a.ButtonPresses);}
    [Fact]public void Existing010ProfilesHistoryAndSettingsSurviveMouseLabInitialization()
    {
        var old=new ProfileStore(Database);old.Apply("Gaming");var before=old.History().Count;
        var store=new MouseLabStore(Database);var p=store.Profiles()[0] with {Name="Local custom",DesiredDpi=1600};store.Save(p);
        store.Record("sensitivity","Calibração",new{Cm360=40});
        var reloaded=new MouseLabStore(Database);Assert.Contains(reloaded.Profiles(),x=>x==p);Assert.Single(reloaded.History());
        Assert.Equal("Gaming",new ProfileStore(Database).Current);Assert.Equal(before,old.History().Count);
    }
    [Fact]public void ProfileBackupRoundTripPreservesDeclaredValuesWithoutApplyingWindows()
    {var store=new MouseLabStore(Database);store.Save(store.Profiles()[0] with{DesiredPolling=8000,SensorNotes="Só valores pretendidos"});var json=store.ExportProfiles();var other=new MouseLabStore(Path.Combine(root,"other.db"));other.ImportProfiles(json);Assert.Equal(store.Profiles(),other.Profiles());}
    [Fact]public void InvalidImportIsAtomicAndRejectsNewSchemaDuplicatesAndOversize()
    {var store=new MouseLabStore(Database);var original=store.ExportProfiles();var p=store.Profiles()[0];Assert.Throws<ArgumentException>(()=>store.ImportProfiles(JsonSerializer.Serialize(new MouseProfileBackup(1,[p with{Name="new"},p with{DesiredDpi=-1}]))));Assert.Equal(original,store.ExportProfiles());Assert.Throws<InvalidDataException>(()=>store.ImportProfiles(JsonSerializer.Serialize(new MouseProfileBackup(2,[p]))));Assert.Throws<InvalidDataException>(()=>store.ImportProfiles(JsonSerializer.Serialize(new MouseProfileBackup(1,[p,p]))));Assert.Throws<InvalidDataException>(()=>store.ImportProfiles(new string(' ',MouseLabStore.MaxImportBytes+1)));}
    [Theory][InlineData(AimMode.Flick)][InlineData(AimMode.Tracking)][InlineData(AimMode.Microadjustment)][InlineData(AimMode.Reaction)][InlineData(AimMode.TargetSwitching)]
    public void ExercisesHaveRealSessionClockAndEndAtThirtySeconds(AimMode mode)
    {var exercise=new AimExercise(mode,2,new Random(42));exercise.Tick(.5);if(exercise.Visible && mode!=AimMode.Tracking)Assert.True(exercise.Click(exercise.X,exercise.Y,.6));exercise.Tick(31);Assert.True(exercise.Finished);var result=exercise.Result(1,800);Assert.Equal(30,result.DurationSeconds);Assert.Equal(mode,result.Mode);result.Validate();}
    [Fact]public void ReactionWaitsAndFalseStartsCountAsMisses()
    {var e=new AimExercise(AimMode.Reaction,1,new Random(1));Assert.False(e.Visible);Assert.False(e.Click(e.X,e.Y,.1));e.Tick(3);Assert.True(e.Visible);Assert.True(e.Click(e.X,e.Y,3.1));var r=e.Result(1,800);Assert.Equal(1,r.Hits);Assert.Equal(1,r.Misses);Assert.True(r.MeanResponseMs>0);}
    [Fact]public void TrackingDoesNotCreditDispatcherStallsOrPointerOutsideArea()
    {var e=new AimExercise(AimMode.Tracking,1);e.Move(e.X,e.Y);e.Tick(.02);e.Move(null,null);e.Tick(.04);e.Move(e.X,e.Y);e.Tick(10);Assert.Equal(50,e.Result(1,800).TrackingPercent,5);}
    [Fact]public void GenericProviderDoesNotClaimUnsupportedHardwareWrites()
    {var provider=new WindowsMouseProvider();var device=new MouseDevice("test","test",null,null,"unknown",provider.Name);Assert.All(provider.Capabilities(device),c=>Assert.NotEqual("ReadWrite",c.Access.ToString()));Assert.Null(device.Dpi);Assert.Null(device.Sensor);}
    [Fact]public void ActualWindowsEnumerationIsReadOnlyAndMayHaveNoMouse()
    {if(!OperatingSystem.IsWindows())return;var devices=new WindowsMouseProvider().Detect();Assert.All(devices,d=>{Assert.NotEmpty(d.Id);Assert.NotEmpty(d.Name);Assert.Null(d.Dpi);Assert.Null(d.ConfiguredPolling);});}
    [Fact]public void FullEightKhzCaptureHasBoundedProcessingMemoryAndGraph()
    {
        var samples=Enumerable.Range(0,MouseAnalyzer.MaximumSamples).Select(i=>new MouseSample(i*.125,1,0,0,0)).ToArray();
        var allocated=GC.GetAllocatedBytesForCurrentThread();var watch=System.Diagnostics.Stopwatch.StartNew();
        var result=MouseAnalyzer.Analyze(samples);watch.Stop();
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread()-allocated,0,64*1024*1024);
        Assert.True(watch.Elapsed<TimeSpan.FromSeconds(5),"Bounded software analysis exceeded the test limit; this is not a game performance benchmark.");
        Assert.Equal(8000,result.EventsPerSecond);Assert.InRange(result.IntervalGraph.Length,1,240);
    }
    [Fact]public void FixturePoolCleanupReleasesItsFilesWithoutClosingAnotherFixture()
    {
        var firstRoot=Path.Combine(root,"first");var first=new MouseLabStore(Path.Combine(firstRoot,"first.db"));
        var other=new MouseLabStore(Path.Combine(root,"other.db"));var before=other.Profiles().Count;
        TestSqliteCleanup.ReleasePools(firstRoot);Directory.Delete(firstRoot,true);
        Assert.Equal(before,other.Profiles().Count);
    }
    [Fact]public void ExplicitGameProfileAndNeverModeSurviveBackupWithoutFallingBackToAnotherProfile()
    {
        var store=new MouseLabStore(Database);var p=MouseProfile.Defaults[0] with{Name="My CS2",Activation=MouseActivation.Never};store.Save(p);
        Assert.Equal(p,store.GameProfiles().Single(x=>x.Game=="CS2"));
        var other=new MouseLabStore(Path.Combine(root,"copy.db"));other.ImportProfiles(store.ExportProfiles());Assert.Equal(p,other.GameProfiles().Single(x=>x.Game=="CS2"));
        Assert.Throws<InvalidDataException>(()=>other.ImportProfiles(JsonSerializer.Serialize(new MouseProfileBackup(1,[p],new(){{"FiveM",p.Name}}))));
    }
}
