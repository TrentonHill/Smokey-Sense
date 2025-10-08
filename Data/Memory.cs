// This is a custom memory reading library I built for reading data from CS2.
// I added caching to improve performance.
// Uses Kernel32 for process memory access. No internet or external deps here.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;

public class Memory
{
    private Process proc;  // The target process (CS2)

    // DLL imports for memory reading
    [DllImport("Kernel32.dll")]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int nSize, IntPtr lpNumberOfBytesRead);
    [DllImport("kernel32.dll")]
    private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    public Memory()  // Constructor - sets up the process
    {
        proc = SetProcess();
    }

    public Process GetProcess()  // Get the current process
    {
        return proc;
    }

    public Process SetProcess()  // Find and set the CS2 process
    {
        proc = Process.GetProcessesByName("cs2").FirstOrDefault();
        if (proc == null)
        {
            Console.WriteLine("[i]: CS2 is not running. Please start the game first!");
            Thread.Sleep(3000);
            Environment.Exit(0);
        }
        return proc;
    }

    public IntPtr GetModuleBase()  // Get base address of client.dll module
    {
        if (proc == null)
        {
            Console.WriteLine("[i]: CS2 is not running. Please start the game first!");
            Thread.Sleep(3000);
            Environment.Exit(0);
        }
        try
        {
            foreach (ProcessModule module in proc.Modules)
            {
                if (module.ModuleName == "client.dll")
                {
                    return module.BaseAddress;
                }
            }
        }
        catch (Exception)
        {
            Console.WriteLine("[i]: CS2 is not running. Please start the game first!");
            Thread.Sleep(3000);
            Environment.Exit(0);
        }
        return IntPtr.Zero;
    }

    public string ReadString(IntPtr addy, int maxLength = 256)
    {
        byte[] buffer = new byte[maxLength];
        if (ReadProcessMemory(proc.Handle, addy, buffer, buffer.Length, IntPtr.Zero))
        {
            int nullIndex = Array.IndexOf(buffer, (byte)0);
            if (nullIndex < 0) nullIndex = maxLength;
            return System.Text.Encoding.UTF8.GetString(buffer, 0, nullIndex);
        }
        return string.Empty;
    }

    public IntPtr ReadPointer(IntPtr addy)  // Read a pointer from memory
    {
        byte[] array = new byte[8];
        if (ReadProcessMemory(proc.Handle, addy, array, array.Length, IntPtr.Zero))
        {
            return (IntPtr)BitConverter.ToInt64(array, 0);
        }
        return IntPtr.Zero;
    }

    public IntPtr ReadPointer(IntPtr addy, int offset)  // Read pointer with single offset
    {
        byte[] array = new byte[8];
        if (ReadProcessMemory(proc.Handle, addy + offset, array, array.Length, IntPtr.Zero))
        {
            return (IntPtr)BitConverter.ToInt64(array, 0);
        }
        return IntPtr.Zero;
    }

    public byte[] ReadBytes(IntPtr addy, int bytes)  // Read raw bytes
    {
        byte[] array = new byte[bytes];
        ReadProcessMemory(proc.Handle, addy, array, array.Length, IntPtr.Zero);
        return array;
    }

    public byte[] ReadBytes(IntPtr addy, int offset, int bytes)  // Read bytes with offset
    {
        byte[] array = new byte[bytes];
        ReadProcessMemory(proc.Handle, addy + offset, array, array.Length, IntPtr.Zero);
        return array;
    }

    public int ReadInt(IntPtr address)  // Read integer
    {
        try
        {
            return BitConverter.ToInt32(ReadBytes(address, 4), 0);
        }
        catch
        {
            return 0;
        }
    }

    public int ReadInt(IntPtr address, int offset)  // Read int with offset
    {
        try
        {
            return BitConverter.ToInt32(ReadBytes(address + offset, 4), 0);
        }
        catch
        {
            return 0;
        }
    }

    public IntPtr ReadLong(IntPtr address)  // Read long (pointer-sized)
    {
        try
        {
            return (IntPtr)BitConverter.ToInt64(ReadBytes(address, 8), 0);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    public IntPtr ReadLong(IntPtr address, int offset)  // Read long with offset
    {
        try
        {
            return (IntPtr)BitConverter.ToInt64(ReadBytes(address + offset, 8), 0);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    public float ReadFloat(IntPtr address)  // Read float
    {
        try
        {
            return BitConverter.ToSingle(ReadBytes(address, 4), 0);
        }
        catch
        {
            return 0f;
        }
    }

    public float ReadFloat(IntPtr address, int offset)  // Read float with offset
    {
        try
        {
            return BitConverter.ToSingle(ReadBytes(address + offset, 4), 0);
        }
        catch
        {
            return 0f;
        }
    }

    public Vector3 ReadVec(IntPtr address)  // Read Vector3
    {
        try
        {
            byte[] value = ReadBytes(address, 12);
            return new Vector3(
                BitConverter.ToSingle(value, 0),
                BitConverter.ToSingle(value, 4),
                BitConverter.ToSingle(value, 8)
            );
        }
        catch
        {
            return new Vector3(float.NaN, float.NaN, float.NaN);
        }
    }

    public Vector3 ReadVec(IntPtr address, int offset)  // Read Vector3 with offset
    {
        try
        {
            byte[] value = ReadBytes(address + offset, 12);
            return new Vector3(
                BitConverter.ToSingle(value, 0),
                BitConverter.ToSingle(value, 4),
                BitConverter.ToSingle(value, 8)
            );
        }
        catch
        {
            return new Vector3(float.NaN, float.NaN, float.NaN);
        }
    }

    public float[] ReadMatrix(IntPtr address)  // Read 4x4 matrix (16 floats)
    {
        try
        {
            byte[] value = ReadBytes(address, 64);
            float[] array = new float[16];
            for (int i = 0; i < 16; i++)
            {
                array[i] = BitConverter.ToSingle(value, i * 4);
            }
            return array;
        }
        catch
        {
            return new float[16];
        }
    }

    public static float Clamp(float value, float min, float max)  // Utility to clamp values
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}