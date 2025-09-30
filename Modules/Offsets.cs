// This is all the offsets used for reading memory from the CS2.
// These offsets are automatically updated on startup by fetching the latest values from a public GitHub repository (see OffsetGrabber.cs).
// I've structured this as a static class for easy access throughout the project.
// All offsets start at 0x00 and get populated dynamically.
using System;
using System.Threading.Tasks;
using CS2Dumper.Offsets;
using static CS2Dumper.Schemas.ClientDll;

namespace Microsoft.COM.Surogate.Data
{
    public static class Offsets
    {
        public static async Task UpdateOffsets()
        {
            m_iHealth = C_BaseEntity.m_iHealth;
            dwViewMatrix = ClientDll.dwViewMatrix;
            m_vecViewOffset = C_BaseModelEntity.m_vecViewOffset;
            m_lifeState = C_BaseEntity.m_lifeState;
            m_vOldOrigin = C_BasePlayerPawn.m_vOldOrigin;
            m_iTeamNum = C_BaseEntity.m_iTeamNum;
            m_hPlayerPawn = CBasePlayerController.m_hPawn;
            dwLocalPlayerPawn = ClientDll.dwLocalPlayerPawn;
            dwEntityList = ClientDll.dwEntityList;
            m_modelState = CSkeletonInstance.m_modelState;
            m_pGameSceneNode = C_BaseEntity.m_pGameSceneNode;

            shotsFired = C_CSPlayerPawn.m_iShotsFired;
            punchAngle = C_CSPlayerPawn.m_aimPunchAngle;
            punchAngleVel = C_CSPlayerPawn.m_aimPunchAngleVel;
            punchTickBase = C_CSPlayerPawn.m_aimPunchTickBase;
            punchTickFraction = C_CSPlayerPawn.m_aimPunchTickFraction;
            punchCacheAddr = C_CSPlayerPawn.m_aimPunchCache;
            punchCacheCount = punchCacheAddr + 0x8;

            await Task.CompletedTask;
        }

        public static int m_iHealth = 0x00;  // Player's current health
        public static int dwViewMatrix = 0x00;  // View matrix for world to screen calculations\
        public static int m_vecViewOffset = 0x00;  // View offset vector
        public static int m_lifeState = 0x00;  // Life state (alive/dead)
        public static int m_vOldOrigin = 0x00;  // Old origin position
        public static int m_iTeamNum = 0x00;  // Team number
        public static int m_hPlayerPawn = 0x00;  // Handle to player pawn
        public static int dwLocalPlayerPawn = 0x00;  // Local player pawn address
        public static int dwEntityList = 0x00;  // Entity list pointer
        public static int m_modelState = 0x00;  // Model state
        public static int m_pGameSceneNode = 0x00;  // Game scene node pointer

        // Recoil Control offsets
        public static int shotsFired = 0x00;
        public static int punchAngle = 0x00;
        public static int punchAngleVel = 0x00;
        public static int punchTickBase = 0x00;
        public static int punchTickFraction = 0x00;
        public static int punchCacheAddr = 0x00;
        public static int punchCacheCount = 0x00;
    }
}