using System;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

public class VectorFieldGridTests
{
    static VectorFieldGrid Grid(bool missing = false)
    {
        var values = new Vector3[8]; var valid = new bool[8];
        for (int z = 0; z < 2; z++) for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
        { int i = x + 2 * (y + 2 * z); values[i] = new Vector3(x * 2,y * 3,z * 4); valid[i] = true; }
        if (missing) valid[7] = false;
        return new VectorFieldGrid(new[] {-2f,2f},new[] {-9f,3f},new[] {10f,20f},values,valid);
    }
    [Test] public void CartesianSamplingUsesXYZAndNonuniformExtents()
    { Assert.That(Grid().TrySample(new Vector3(0,-3,15),out var v)); Assert.That(v,Is.EqualTo(new Vector3(1,1.5f,2))); }
    [Test] public void ZeroAndMissingAreDifferent()
    {
        Assert.That(Grid().TrySample(new Vector3(-2,-9,10),out var zero)); Assert.That(zero,Is.EqualTo(Vector3.zero));
        Assert.That(Grid(true).TrySample(new Vector3(0,-3,15),out _),Is.False);
    }
    [Test] public void InputsAreOwnedAndNotShared()
    {
        var values = new Vector3[8]; var x = new[] {0f,1f};
        var grid = new VectorFieldGrid(x,new[] {0f,1f},new[] {0f,1f},values);
        values[0]=Vector3.one; x[0]=99;
        Assert.That(grid.Min,Is.EqualTo(Vector3.zero)); Assert.That(grid.Value(0,0,0),Is.EqualTo(Vector3.zero));
        var copy=grid.CopyAxis(0);copy[0]=99;Assert.That(grid.Min.x,Is.EqualTo(0));
    }
    [Test] public void MappingDoesNotChangePhysicalSpeed()
    {
        var map=VectorFieldMapping.Identity;map.origin=new Vector3(-2,-9,10);map.positionUnitsToLocal=new Vector3(2,3,4);map.velocityUnitsToPositionUnits=.1f;
        var frame=new VectorFieldFrame(Grid(),map);var p=map.ToLocal(new Vector3(0,-3,15));
        Assert.That(frame.SamplePhysicalVector(p),Is.EqualTo(new Vector3(1,1.5f,2)));
        Assert.That((frame.SampleWorldVelocity(p)-new Vector3(.2f,.45f,.8f)).magnitude,Is.LessThan(1e-6f));
    }
    [Test] public void InvalidAxesAndScalesAreRejected()
    {
        Assert.Throws<ArgumentException>(()=>new VectorFieldGrid(new[]{0f,0f},new[]{0f,1f},new[]{0f,1f},new Vector3[8]));
        var map=VectorFieldMapping.Identity;map.positionUnitsToLocal.y=0;Assert.Throws<ArgumentException>(()=>map.Validate());
    }
    [TestCase(false,17,3,51)] [TestCase(false,200,3,100)]
    public void FixedCountRespectsUnitAndCap(bool automatic,int number,int unit,int expected)
    { Assert.That(VectorFieldSpawnTable.ResolveCount(automatic,number,unit,1,false,Vector3.one,1,100),Is.EqualTo(expected)); }
    [Test] public void AutomaticReferenceAndCapAreIndependent()
    {
        Assert.That(VectorFieldSpawnTable.ResolveCount(true,0,1,24,false,new Vector3(2,3,4),70,100),Is.EqualTo(70));
        Assert.That(VectorFieldSpawnTable.ResolveCount(true,0,1,24,false,new Vector3(2,3,4),170,100),Is.EqualTo(100));
        Assert.That(VectorFieldSpawnTable.ResolveCount(true,0,1,8,true,new Vector3(2,999,4),70,100),Is.EqualTo(70));
    }
    [Test] public void MissingAndTransparentCellsDoNotAllocate()
    {
        var ramp=new[]{Color.white,Color.white};var frame=new VectorFieldFrame(Grid(true),VectorFieldMapping.Identity);
        Assert.That(VectorFieldSpawnTable.Build(frame,ramp,0,10,1,0).Cells.Length,Is.EqualTo(0));
        ramp=new[]{Color.clear,Color.clear};frame=new VectorFieldFrame(Grid(),VectorFieldMapping.Identity);
        Assert.That(VectorFieldSpawnTable.Build(frame,ramp,0,10,1,0).Cells.Length,Is.EqualTo(0));
    }
    [Test] public void SpawnBuildHonoursCancellation()
    {
        var token=new CancellationToken(true);
        Assert.Throws<OperationCanceledException>(()=>VectorFieldSpawnTable.Build(new VectorFieldFrame(Grid(),VectorFieldMapping.Identity),new[]{Color.white,Color.white},0,10,1,0,token));
    }
    [Test] public void PlaneConstrainsMotionWithoutChangingPhysicalVector()
    {
        var values=new[]{new Vector3(1,7,2),new Vector3(1,7,2),new Vector3(1,7,2),new Vector3(1,7,2)};
        var grid=new VectorFieldGrid(new[]{0f,1f},new[]{-4f},new[]{0f,1f},values);
        var frame=new VectorFieldFrame(grid,VectorFieldMapping.Identity);
        Assert.That(frame.SamplePhysicalVector(new Vector3(.5f,-4,.5f)),Is.EqualTo(new Vector3(1,7,2)));
        Assert.That(frame.SampleWorldVelocity(new Vector3(.5f,-4,.5f)),Is.EqualTo(new Vector3(1,0,2)));
        Assert.That(frame.TrySample(new Vector3(.5f,0,.5f),out _),Is.False);
    }
    [Test] public void VerticalOnlyPlaneHasNoMovingSpawnArea()
    {
        var grid=new VectorFieldGrid(new[]{0f,1f},new[]{0f},new[]{0f,1f},new[]{Vector3.up,Vector3.up,Vector3.up,Vector3.up});
        var table=VectorFieldSpawnTable.Build(new VectorFieldFrame(grid,VectorFieldMapping.Identity),new[]{Color.white,Color.white},0,10,1,0);
        Assert.That(table.Cells.Length,Is.EqualTo(0));Assert.That(table.VisibleMeasure,Is.EqualTo(0));
    }
}
