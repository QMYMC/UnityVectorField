using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class VectorFieldTextTests
{
    string path;
    [SetUp] public void Setup() { path=Path.GetTempFileName(); }
    [TearDown] public void Teardown() { File.Delete(path); }
    [Test] public void OmittedRowsAreMissingAndExplicitZeroIsValid()
    {
        File.WriteAllText(path,"0 0 0 0 0 0 1\n1 0 0 1 0 0 1\n0 0 1 1 0 0 1\n1 0 1 1 0 0 1\n2 0 0 1 0 0 1\n");
        var grid=VectorFieldText.Read(path).Grid;
        Assert.That(grid.TrySample(new Vector3(0,0,0),out var v)); Assert.That(v,Is.EqualTo(Vector3.zero));
        Assert.That(grid.Valid(2,0,1),Is.False);Assert.That(grid.TrySample(new Vector3(1.5f,0,.5f),out _),Is.False);
    }
    [Test] public void DuplicateCoordinatesAreRejected()
    { File.WriteAllText(path,"0 0 0 1 0 0 1\n0 0 0 1 0 0 1\n");Assert.Throws<FormatException>(()=>VectorFieldText.Read(path)); }
    [Test] public void InvalidValidityColumnIsRejected()
    { File.WriteAllText(path,"0 0 0 1 0 0 2\n");Assert.Throws<FormatException>(()=>VectorFieldText.Read(path)); }
    [Test] public void InvalidNonfiniteSamplesAreNotTurnedIntoMissing()
    { File.WriteAllText(path,"0 0 0 NaN 0 0 1\n1 0 0 1 0 0 1\n0 0 1 1 0 0 1\n1 0 1 1 0 0 1\n");Assert.Throws<ArgumentException>(()=>VectorFieldText.Read(path)); }
}
