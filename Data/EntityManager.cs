// Manages fetching and updating entities from memory.
// I use locking for thread safety since updates happen in a loop.
// Also handles world to screen and bone reading.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Windows.Forms;
using Microsoft.COM.Surogate.Data;

public class EntityManager
{
    // Enum for bone IDs - these are standard for CS2 skeletons
    public enum BoneIds
    {
        Pelvis = 0,
        Spine1 = 5,
        Spine2 = 6,
        Spine3 = 7,
        Neck = 8,
        Head = 9,
        ClavicleLeft = 13,
        UpperArmLeft = 14,
        LowerArmLeft = 15,
        HandLeft = 16,
        ClavicleRight = 17,
        UpperArmRight = 18,
        LowerArmRight = 19,
        HandRight = 20,
        KneeLeft = 23,
        FootLeft = 24,
        KneeRight = 26,
        FootRight = 27
    }

    private readonly Memory memory;  // Memory reader instance
    private readonly object entityLock = new object();  // Lock for thread safe access
    private Entity localPlayer;  // Local player entity
    private List<Entity> entities;  // List of all entities
    private float[] cachedViewMatrix;  // Cached view matrix for W2S
    private int[] boneIndices;  // Indices of bones to read

    public Entity LocalPlayer  // Thread safe getter for local player
    {
        get
        {
            lock (entityLock)
            {
                return localPlayer;
            }
        }
    }

    public List<Entity> Entities  // Thread safe getter for entities
    {
        get
        {
            lock (entityLock)
            {
                return new List<Entity>(entities);
            }
        }
    }

    public EntityManager(Memory memory)  // Constructor
    {
        this.memory = memory;
        localPlayer = new Entity();
        entities = new List<Entity>();
        cachedViewMatrix = new float[16];
        boneIndices = (int[])Enum.GetValues(typeof(BoneIds));
    }

    public Entity GetLocalPlayer()  // Fetch local player data
    {
        lock (entityLock)
        {
            IntPtr pawnPtr = IntPtr.Zero;
            for (int i = 0; i < 3; i++)  // Retry a few times if read fails
            {
                pawnPtr = memory.ReadPointer(memory.GetModuleBase() + Offsets.dwLocalPlayerPawn);
                if (pawnPtr != IntPtr.Zero) break;
                Thread.Sleep(1);
            }
            if (pawnPtr == IntPtr.Zero) return new Entity();

            Vector3 pos = memory.ReadVec(pawnPtr, Offsets.m_vOldOrigin);
            if (float.IsNaN(pos.X) || float.IsNaN(pos.Y) || float.IsNaN(pos.Z)) return new Entity();

            return new Entity
            {
                PawnAddress = pawnPtr,
                position = pos,
                origin = pos,
                view = memory.ReadVec(pawnPtr, Offsets.m_vecViewOffset),
                team = memory.ReadInt(pawnPtr, Offsets.m_iTeamNum),
                health = memory.ReadInt(pawnPtr, Offsets.m_iHealth)
            };
        }
    }

    public List<Entity> GetEntities()  // Fetch all entities
    {
        lock (entityLock)
        {
            cachedViewMatrix = memory.ReadMatrix(memory.GetModuleBase() + Offsets.dwViewMatrix);
            List<Entity> entityList = new List<Entity>();
            IntPtr moduleBase = memory.GetModuleBase();
            IntPtr entityListPtr = IntPtr.Zero;
            for (int i = 0; i < 3; i++)
            {
                entityListPtr = memory.ReadPointer(moduleBase + Offsets.dwEntityList);
                if (entityListPtr != IntPtr.Zero) break;
                Thread.Sleep(1);
            }
            if (entityListPtr == IntPtr.Zero)
            {
                entities.Clear();
                return entityList;
            }

            IntPtr listEntry = memory.ReadPointer(entityListPtr + 16);
            if (listEntry == IntPtr.Zero)
            {
                entities.Clear();
                return entityList;
            }

            Entity local = GetLocalPlayer();
            for (int j = 0; j < 64; j++)  // Loop through possible players (up to 64)
            {
                IntPtr controller = memory.ReadPointer(listEntry, j * 120);
                if (controller == IntPtr.Zero) continue;

                int pawnHandle = memory.ReadInt(controller, Offsets.m_hPlayerPawn);
                if (pawnHandle == 0) continue;

                IntPtr listEntry2 = memory.ReadPointer(entityListPtr, 8 * ((pawnHandle & 0x7FFF) >> 9) + 16);
                if (listEntry2 == IntPtr.Zero) continue;

                IntPtr pawn = IntPtr.Zero;
                for (int k = 0; k < 3; k++)
                {
                    pawn = memory.ReadPointer(listEntry2, 120 * (pawnHandle & 0x1FF));
                    if (pawn != IntPtr.Zero) break;
                    Thread.Sleep(1);
                }

                if (pawn == IntPtr.Zero || pawn == local.PawnAddress || memory.ReadInt(pawn, Offsets.m_iHealth) <= 0) continue;

                Entity ent = PopulateEntity(pawn, local);
                if (ent != null) entityList.Add(ent);
            }

            entities = entityList;
            return new List<Entity>(entities);
        }
    }

    private Entity PopulateEntity(IntPtr pawnAddress, Entity localPlayer)  // Fill entity data
    {
        Vector3 pos = memory.ReadVec(pawnAddress, Offsets.m_vOldOrigin);
        Vector3 viewOffset = memory.ReadVec(pawnAddress, Offsets.m_vecViewOffset);
        if (float.IsNaN(pos.X) || float.IsNaN(pos.Y) || float.IsNaN(pos.Z) ||
            float.IsNaN(viewOffset.X) || float.IsNaN(viewOffset.Y) || float.IsNaN(viewOffset.Z))
            return null;

        Vector2 screenSize = new Vector2(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
        Vector2 pos2D = WorldToScreen(cachedViewMatrix, pos, screenSize);
        Vector2 head2D = WorldToScreen(cachedViewMatrix, Vector3.Add(pos, viewOffset), screenSize);
        if (pos2D.X == -99f || head2D.X == -99f) return null;

        IntPtr sceneNode = memory.ReadPointer(pawnAddress, Offsets.m_pGameSceneNode);
        if (sceneNode == IntPtr.Zero) return null;

        IntPtr boneArray = memory.ReadPointer(sceneNode, Offsets.m_modelState + 128);
        if (boneArray == IntPtr.Zero) return null;

        List<Vector3> bones = ReadBones(boneArray);
        if (bones == null || bones.Count == 0) return null;

        List<Vector2> bones2D = ReadBones2D(bones, cachedViewMatrix, screenSize);
        if (bones2D == null || bones2D.Count == 0) return null;

        return new Entity
        {
            PawnAddress = pawnAddress,
            team = memory.ReadInt(pawnAddress, Offsets.m_iTeamNum),
            health = memory.ReadInt(pawnAddress, Offsets.m_iHealth),
            position = pos,
            origin = pos,
            view = viewOffset,
            position2D = pos2D,
            head2D = head2D,
            distance = Vector3.Distance(memory.ReadVec(localPlayer.PawnAddress, Offsets.m_vOldOrigin), pos),
            bones = bones,
            bones2D = bones2D
        };
    }

    public static Vector2 WorldToScreen(float[] matrix, Vector3 pos, Vector2 windowSize)  // World to screen conversion
    {
        float w = matrix[12] * pos.X + matrix[13] * pos.Y + matrix[14] * pos.Z + matrix[15];
        if (w > 0.001f)
        {
            float x = matrix[0] * pos.X + matrix[1] * pos.Y + matrix[2] * pos.Z + matrix[3];
            float y = matrix[4] * pos.X + matrix[5] * pos.Y + matrix[6] * pos.Z + matrix[7];
            float screenX = windowSize.X / 2f + windowSize.X / 2f * x / w;
            float screenY = windowSize.Y / 2f - windowSize.Y / 2f * y / w;
            return new Vector2(screenX, screenY);
        }
        return new Vector2(-99f, -99f);
    }

    public void UpdateLocalPlayer(Entity newLocalPlayer)  // Update local player
    {
        lock (entityLock)
        {
            localPlayer = newLocalPlayer;
        }
    }

    public void UpdateEntities(List<Entity> newEntities)  // Update entity list
    {
        lock (entityLock)
        {
            entities = new List<Entity>(newEntities);
        }
    }

    public List<Vector3> ReadBones(IntPtr boneArray)  // Read bone positions from array (Not perfect)
    {
        try
        {
            byte[] buffer = memory.ReadBytes(boneArray, 896);  // Bone data size
            List<Vector3> boneList = new List<Vector3>(boneIndices.Length);

            foreach (int idx in boneIndices)
            {
                if (idx * 32 + 12 <= buffer.Length)
                {
                    float x = BitConverter.ToSingle(buffer, idx * 32);
                    float y = BitConverter.ToSingle(buffer, idx * 32 + 4);
                    float z = BitConverter.ToSingle(buffer, idx * 32 + 8);
                    if (!float.IsNaN(x) && !float.IsNaN(y) && !float.IsNaN(z))
                    {
                        boneList.Add(new Vector3(x, y, z));
                    }
                }
            }
            return boneList;
        }
        catch
        {
            return new List<Vector3>();
        }
    }

    public static List<Vector2> ReadBones2D(List<Vector3> bones, float[] viewMatrix, Vector2 screenSize)  // Convert bones to 2D
    {
        List<Vector2> bone2DList = new List<Vector2>();
        foreach (Vector3 bone in bones)
        {
            Vector2 pos2D = WorldToScreen(viewMatrix, bone, screenSize);
            bone2DList.Add(pos2D);
        }
        return bone2DList;
    }
}