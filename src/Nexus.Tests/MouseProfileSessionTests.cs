using Nexus.Core;
using Nexus.Optimization;
using Xunit;

namespace Nexus.Tests;

public sealed class MouseProfileSessionTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"nexus-mouse-session-"+Guid.NewGuid().ToString("N"));
    private const string Process="priority:12:34";
    private sealed class Setting(string key,string value):ISystemSetting
    {public string Key=>key;public string Name=>key;public string Value=value;public bool Fail;public string Read()=>Value;public void Write(string v){if(Fail)throw new IOException("Write rejected");Value=v;}}
    private readonly Dictionary<string,Setting> values=new(){[MousePrecision.SpeedKey]=new(MousePrecision.SpeedKey,"12"),[MousePrecision.AccelerationKey]=new(MousePrecision.AccelerationKey,"6,10,1"),[Process]=new(Process,"Normal")};
    private ISystemSetting Resolve(string key)=>values[key];
    private SystemOptimizer Optimizer()=>new(Path.Combine(root,"nexus.db"),Resolve);
    public void Dispose(){TestSqliteCleanup.ReleasePools(root);if(Directory.Exists(root))Directory.Delete(root,true);}
    [Fact]public void ProcessExitRestoresCapturedValuesWithoutChangingPriorityOrHardware()
    {var o=Optimizer();var session=new MouseProfileSession(o,Resolve);session.Start(MouseProfile.Defaults[0],Process);Assert.True(session.Active);Assert.Equal("10",values[MousePrecision.SpeedKey].Value);Assert.Equal("0,0,0",values[MousePrecision.AccelerationKey].Value);Assert.Equal("Normal",values[Process].Value);values[Process].Value="ended";session.Poll();Assert.False(session.Active);Assert.Equal("12",values[MousePrecision.SpeedKey].Value);Assert.Equal("6,10,1",values[MousePrecision.AccelerationKey].Value);Assert.All(o.History(),x=>Assert.Equal("reverted",x.State));}
    [Fact]public void ReopenedInstanceRequiresExplicitRecoveryAndDoesNotAutoRestoreLiveOwner()
    {var o=Optimizer();var owner=new MouseProfileSession(o,Resolve);owner.Start(MouseProfile.Defaults[0],Process);var reopened=new MouseProfileSession(o,Resolve);Assert.True(reopened.RecoveryRequired);values[Process].Value="ended";reopened.Poll();Assert.True(owner.Active);Assert.True(reopened.End().Complete);Assert.False(owner.Active);}
    [Fact]public void FailedSecondSettingRollsBackFirstAndClosesSession()
    {var o=Optimizer();var session=new MouseProfileSession(o,Resolve);values[MousePrecision.AccelerationKey].Fail=true;Assert.Throws<IOException>(()=>session.Start(MouseProfile.Defaults[0],Process));Assert.Equal("12",values[MousePrecision.SpeedKey].Value);Assert.False(session.Active);}
    [Fact]public void ExternalChangeLeavesRecoverableSessionAndNeverBlindlyOverwrites()
    {var o=Optimizer();var session=new MouseProfileSession(o,Resolve);session.Start(MouseProfile.Defaults[0],Process);values[MousePrecision.SpeedKey].Value="15";var result=session.End();Assert.False(result.Complete);Assert.True(session.Active);Assert.Equal("15",values[MousePrecision.SpeedKey].Value);values[MousePrecision.SpeedKey].Value="10";Assert.True(session.End().Complete);}
    [Fact]public void EndedProcessAndDuplicateSessionCannotApply()
    {var o=Optimizer();var session=new MouseProfileSession(o,Resolve);values[Process].Value="ended";Assert.Throws<InvalidOperationException>(()=>session.Start(MouseProfile.Defaults[0],Process));Assert.Empty(o.History());values[Process].Value="Normal";session.Start(MouseProfile.Defaults[0],Process);Assert.Throws<InvalidOperationException>(()=>session.Start(MouseProfile.Defaults[1],Process));Assert.True(session.End().Complete);}
    [Fact]public void ProfileWithAccelerationUncheckedLeavesExistingThresholdsAlone()
    {var prepared=MouseProfileSession.Prepare(MouseProfile.Defaults[2],Resolve);Assert.Single(prepared);Assert.Equal(MousePrecision.SpeedKey,prepared[0].Key);}
}
