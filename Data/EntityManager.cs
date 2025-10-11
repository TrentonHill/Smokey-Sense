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

    // Passage à un modèle "snapshot swap": pas de lock, on remplace la référence en entier.
    // L'overlay lit ces références sans les modifier.
    private volatile Entity localPlayer;   // Local player snapshot
    private volatile List<Entity> entities; // Entities snapshot
    private float[] cachedViewMatrix;  // Cached view matrix for W2S (par frame dans GetEntities)

    public Entity LocalPlayer => localPlayer;          // accès lock-free
    public List<Entity> Entities => entities;          // accès lock-free (ne pas modifier côté appelant)

    public EntityManager(Memory memory)  // Constructor
    {
        this.memory = memory;
        localPlayer = new Entity();
        entities = new List<Entity>(32);
        cachedViewMatrix = new float[16];
    }

    public Entity GetLocalPlayer()  // Fetch local player data (appelé par le thread d’update)
    {
        IntPtr pawnPtr = memory.ReadPointer(memory.GetModuleBase() + Offsets.dwLocalPlayerPawn);
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

    public List<Entity> GetEntities()  // Fetch all entities (appelé par le thread d’update)
    {
        IntPtr moduleBase = memory.GetModuleBase();
        cachedViewMatrix = memory.ReadMatrix(moduleBase + Offsets.dwViewMatrix);

        var entityList = new List<Entity>(32);

        IntPtr entityListPtr = memory.ReadPointer(moduleBase + Offsets.dwEntityList);
        if (entityListPtr == IntPtr.Zero)
        {
            entities = entityList; // vide
            return entities;
        }

        IntPtr listEntry = memory.ReadPointer(entityListPtr + 16);
        if (listEntry == IntPtr.Zero)
        {
            entities = entityList; // vide
            return entities;
        }

        // Lire le local une seule fois
        Entity local = GetLocalPlayer();

        for (int j = 0; j < 64; j++)  // Loop through possible players (up to 64)
        {
            IntPtr controller = memory.ReadPointer(listEntry, j * 120);
            if (controller == IntPtr.Zero) continue;

            int pawnHandle = memory.ReadInt(controller, Offsets.m_hPlayerPawn);
            if (pawnHandle == 0) continue;

            IntPtr listEntry2 = memory.ReadPointer(entityListPtr, 8 * ((pawnHandle & 0x7FFF) >> 9) + 16);
            if (listEntry2 == IntPtr.Zero) continue;

            // Éviter les retries + Sleep ici: si null, on passe
            IntPtr pawn = memory.ReadPointer(listEntry2, 120 * (pawnHandle & 0x1FF));
            if (pawn == IntPtr.Zero || pawn == local.PawnAddress) continue;

            int health = memory.ReadInt(pawn, Offsets.m_iHealth);
            if (health <= 0) continue;

            int team = memory.ReadInt(pawn, Offsets.m_iTeamNum);

            Entity ent = PopulateEntity(pawn, local, team, health);
            if (ent != null) entityList.Add(ent);
        }

        // Swap du snapshot (pas de copie)
        entities = entityList;
        return entities;
    }

    // Ajout de team/health pour éviter de relire la mémoire
    private Entity PopulateEntity(IntPtr pawnAddress, Entity localPlayer, int team, int health)  // Fill entity data
    {
        Vector3 pos = memory.ReadVec(pawnAddress, Offsets.m_vOldOrigin);
        Vector3 viewOffset = memory.ReadVec(pawnAddress, Offsets.m_vecViewOffset);
        if (float.IsNaN(pos.X) || float.IsNaN(pos.Y) || float.IsNaN(pos.Z) ||
            float.IsNaN(viewOffset.X) || float.IsNaN(viewOffset.Y) || float.IsNaN(viewOffset.Z))
            return null;

        Vector2 screenSize = new Vector2(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
        Vector2 head2D = WorldToScreen(cachedViewMatrix, Vector3.Add(pos, viewOffset), screenSize);
        if (head2D.X == -99f) return null;

        IntPtr sceneNode = memory.ReadPointer(pawnAddress, Offsets.m_pGameSceneNode);
        if (sceneNode == IntPtr.Zero) return null;

        IntPtr boneArray = memory.ReadPointer(sceneNode, Offsets.m_modelState + 128);
        if (boneArray == IntPtr.Zero) return null;

        // Lecture des os + proj 2D
        List<Vector3> bones = ReadBones(boneArray);
        if (bones == null || bones.Count == 0) return null;

        List<Vector2> bones2D = ReadBones2D(bones, cachedViewMatrix, screenSize);
        if (bones2D == null || bones2D.Count == 0) return null;

        return new Entity
        {
            PawnAddress = pawnAddress,
            team = team,
            health = health,
            position = pos,
            origin = pos,
            view = viewOffset,
            head2D = head2D,
            distance = Vector3.Distance(localPlayer.position, pos),
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

    public void UpdateLocalPlayer(Entity newLocalPlayer)  // Update local player (swap snapshot)
    {
        localPlayer = newLocalPlayer;
    }

    public void UpdateEntities(List<Entity> newEntities)  // Update entity list (swap snapshot)
    {
        entities = newEntities ?? new List<Entity>(0);
    }

    public List<Vector3> ReadBones(IntPtr boneArray)  // Read bone positions from array (optimisé)
    {
        try
        {
            // Lecture brute
            byte[] buffer = memory.ReadBytes(boneArray, 896);  // Bone data size
            // Convertir en float[] une seule fois (évite BitConverter.ToSingle * N)
            int floatCount = buffer.Length / 4;
            float[] flts = new float[floatCount];
            Buffer.BlockCopy(buffer, 0, flts, 0, buffer.Length);

            var boneList = new List<Vector3>(18);
            int[] boneIndices = { 0, 5, 6, 7, 8, 9, 13, 14, 15, 16, 17, 18, 19, 20, 23, 24, 26, 27 };

            for (int i = 0; i < boneIndices.Length; i++)
            {
                int idx = boneIndices[i];
                int baseFloatIndex = (idx * 32) / 4; // 32 bytes stride -> 8 floats stride
                if (baseFloatIndex + 2 < flts.Length)
                {
                    float x = flts[baseFloatIndex + 0];
                    float y = flts[baseFloatIndex + 1];
                    float z = flts[baseFloatIndex + 2];
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
            return new List<Vector3>(0);
        }
    }

    public static List<Vector2> ReadBones2D(List<Vector3> bones, float[] viewMatrix, Vector2 screenSize)  // Convert bones to 2D
    {
        var bone2DList = new List<Vector2>(bones.Count);
        for (int i = 0; i < bones.Count; i++)
        {
            Vector2 pos2D = WorldToScreen(viewMatrix, bones[i], screenSize);
            bone2DList.Add(pos2D);
        }
        return bone2DList;
    }
}