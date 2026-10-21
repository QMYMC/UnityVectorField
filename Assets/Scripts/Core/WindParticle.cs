using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct WindParticle
{
    public Vector3 position;
    public Vector3 velocity;
    public float physicalSpeed;
    public float life;
    public float seed;
    public float stretch;
}
