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
    public Vector2 position2D { get; set; }  // 2D screen position
    public Vector2 viewPosition2D { get; set; }  // 2D view position (not used much, but kept for future)
    public Vector3 head { get; set; }  // Head position
    public Vector2 head2D { get; set; }  // 2D head position
    public int LifeState { get; set; }  // Life state
    public int team { get; set; }  // Team ID
    public int health { get; set; }  // Health points
    public bool Visible { get; set; }  // Visibility flag (not implemented yet)
    public float distance { get; set; }  // Distance to local player
    public List<Vector3> bones { get; set; }  // List of bone positions
    public List<Vector2> bones2D { get; set; }  // 2D bone positions for drawing
    public IntPtr HeldWeapon { get; set; }  // Held weapon pointer
    public short WeaponIndex { get; set; }  // Weapon index
    public int Index { get; set; } = -1;// Player index (for identification)
}