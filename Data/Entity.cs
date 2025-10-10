// Holds position, health, team, etc. I kept it basic for efficiency.

using System;
using System.Collections.Generic;
using System.Numerics;

public class Entity
{
    public IntPtr PawnAddress { get; set; }  // Address of the pawn in memory
    public Vector3 position { get; set; }  // Current position
    public Vector3 origin { get; set; }  // Origin position
    public Vector3 view { get; set; }  // View vector
    public Vector2 head2D { get; set; } // 2D head position
    public int LifeState { get; set; }  // Life state
    public int team { get; set; }  // Team ID
    public int health { get; set; }  // Health points
    public float distance { get; set; }  // Distance to local player
    public List<Vector3> bones { get; set; }  // List of bone positions
    public List<Vector2> bones2D { get; set; }  // 2D bone positions for drawing
}