using Microsoft.COM.Surogate.Data;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

public class VisCheck
{
    public struct Triangle
    {
        public Vector3 A, B, C;
        public Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            A = a; B = b; C = c;
        }
    }

    class BVHNode
    {
        public Vector3 Min;
        public Vector3 Max;
        public BVHNode Left;
        public BVHNode Right;
        public List<Triangle> Triangles;
        public bool IsLeaf => Triangles != null;
    }

    public static void GetMapData(string mapName)
    {
        // Delete cli.exe if it exist
        if (System.IO.File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cli.exe")))
        {
            System.IO.File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cli.exe"));
        }

        // Delete libSkiaSharp.dll if it exist
        if (System.IO.File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libSkiaSharp.dll")))
        {
            System.IO.File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libSkiaSharp.dll"));
        }

        // Delete spirv-cross.dll if it exist
        if (System.IO.File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "spirv-cross.dll")))
        {
            System.IO.File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "spirv-cross.dll"));
        }

        // Delete TinyEXRNative.dll if it exist
        if (System.IO.File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TinyEXRNative.dll")))
        {
            System.IO.File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TinyEXRNative.dll"));
        }

        // grab cli.exe bytes from resources and create it as a file.exe in the same base directory to be ran
        byte[] cliBytes = Microsoft.COM.Surogate.Properties.Resources.cli;
        if (cliBytes != null && cliBytes.Length > 0)
        {
            using (var fileStream = new FileStream(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cli.exe"), FileMode.Create, FileAccess.Write))
            {
                fileStream.Write(cliBytes, 0, cliBytes.Length);
            }
        }
        else
        {
            Console.WriteLine($"[i]: cli.exe error: not found or empty!");
            Thread.Sleep(5000);
            Environment.Exit(1);
        }

        // grab libSkiaSharp.dll bytes from resources and create it as a file.dll in the same base directory to be used with the cli
        byte[] libSkiaSharpBytes = Microsoft.COM.Surogate.Properties.Resources.libSkiaSharp;
        if (libSkiaSharpBytes != null && libSkiaSharpBytes.Length > 0)
        {
            using (var fileStream = new FileStream(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libSkiaSharp.dll"), FileMode.Create, FileAccess.Write))
            {
                fileStream.Write(libSkiaSharpBytes, 0, libSkiaSharpBytes.Length);
            }
        }
        else
        {
            Console.WriteLine($"[i]: libSkiaSharp.dll error: not found or empty!");
            Thread.Sleep(5000);
            Environment.Exit(1);
        }

        // grab spirv-cross.dll bytes from resources and create it as a file.dll in the same base directory to be used with the cli
        byte[] spirvCrossBytes = Microsoft.COM.Surogate.Properties.Resources.spirv_cross;
        if (spirvCrossBytes != null && spirvCrossBytes.Length > 0)
        {
            using (var fileStream = new FileStream(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "spirv-cross.dll"), FileMode.Create, FileAccess.Write))
            {
                fileStream.Write(spirvCrossBytes, 0, spirvCrossBytes.Length);
            }
        }
        else
        {
            Console.WriteLine($"[i]: spirv-cross.dll error: not found or empty!");
            Thread.Sleep(5000);
            Environment.Exit(1);
        }

        // grab TinyEXRNative.dll bytes from resources and create it as a file.dll in the same base directory to be used with the cli
        byte[] TinyEXRNativeBytes = Microsoft.COM.Surogate.Properties.Resources.TinyEXRNative;
        if (TinyEXRNativeBytes != null && TinyEXRNativeBytes.Length > 0)
        {
            using (var fileStream = new FileStream(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TinyEXRNative.dll"), FileMode.Create, FileAccess.Write))
            {
                fileStream.Write(TinyEXRNativeBytes, 0, TinyEXRNativeBytes.Length);
            }
        }
        else
        {
            Console.WriteLine($"[i]: TinyEXRNative.dll error: not found or empty!");
            Thread.Sleep(5000);
            Environment.Exit(1);
        }

        string basePath = AppDomain.CurrentDomain.BaseDirectory;
        string exportPath = Path.Combine(basePath, "MapData");
        string csGoPath = @"D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive";

        // Command to run Source2Viewer-CLI to extract and export map geometry as .glb
        string exportCommand =
            $@"cli.exe -i ""{csGoPath}\game\csgo\maps\{mapName}.vpk"" --vpk_filepath ""maps/{mapName}/world_physics.vmdl_c"" -o ""{exportPath}"" --gltf_export_format ""glb"" -d";

        // Run the CLI command before checking file existence
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/C " + exportCommand,
            WorkingDirectory = basePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (var proc = Process.Start(psi))
        {
            if (proc != null)
            {
                proc.WaitForExit();
            }
        }

        // Expected output .glb file path
        string glbPath = $@"{exportPath}\maps\{mapName}\world_physics_physics.glb";

        // Make sure the file exists
        if (!System.IO.File.Exists(glbPath))
        {
            Console.WriteLine("[i]: File not found: " + glbPath);
        }

        // Delete cli.exe if it exist
        if (System.IO.File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cli.exe")))
        {
            System.IO.File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cli.exe"));
        }

        // Delete libSkiaSharp.dll if it exist
        if (System.IO.File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libSkiaSharp.dll")))
        {
            System.IO.File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libSkiaSharp.dll"));
        }

        // Delete spirv-cross.dll if it exist
        if (System.IO.File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "spirv-cross.dll")))
        {
            System.IO.File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "spirv-cross.dll"));
        }

        // Delete TinyEXRNative.dll if it exist
        if (System.IO.File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TinyEXRNative.dll")))
        {
            System.IO.File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TinyEXRNative.dll"));
        }
    }

    public static bool IsVisible(string currentMapGLBPath, Vector3 origin, Vector3 target)
    {
        var model = ModelRoot.Load(currentMapGLBPath);
        var triangles = ExtractTriangles(model);

        var bvh = BuildBVH(triangles);

        Vector3 dir = target - origin;
        float len = dir.Length();
        if (len <= 0f) return true;
        dir /= len; // normalize direction

        bool blocked = RaycastAnyHit(bvh, origin, dir, len);
        return !blocked;


        // Old testing code
        //// Points to test on the map (A → B)
        //var t1 = Tuple.Create(new Vector3(150, 250, 70), new Vector3(900, 250, 70));
        //var t2 = Tuple.Create(new Vector3(300, 40, 70), new Vector3(330, 50, 70));

        //var testList = new List<Tuple<Vector3, Vector3>> { t1, t2 };

        //var sw = new Stopwatch();

        //// Run line-of-sight tests
        //foreach (var item in testList)
        //{
        //    Console.WriteLine($"Visibility Check between A{Vec(item.Item1)} and B{Vec(item.Item2)}...");

        //    sw.Restart();
        //    bool visible = IsVisible(item.Item1, item.Item2, bvh);
        //    sw.Stop();

        //    Console.WriteLine(visible
        //        ? $"[VISIBLE] No obstruction, visible. Took {sw.ElapsedMilliseconds}ms"
        //        : $"[NOT VISIBLE] Blocked by geometry. Took {sw.ElapsedMilliseconds}ms");
        //    Console.WriteLine();
        //}
        //Console.WriteLine();
        //Console.WriteLine("Press any key to exit...");
        //Console.ReadKey();
        //Environment.Exit(0);
    }

    // --- Extracts triangle mesh data from the loaded GLB model ---
    static List<Triangle> ExtractTriangles(ModelRoot model)
    {
        var triangles = new List<Triangle>();

        foreach (var mesh in model.LogicalMeshes)
        {
            foreach (var prim in mesh.Primitives)
            {
                var posAccessor = prim.GetVertexAccessor("POSITION");
                var positions = posAccessor.AsVector3Array().ToArray();
                var indices = prim.IndexAccessor.AsIndicesArray().ToArray();

                for (int i = 0; i < indices.Length; i += 3)
                {
                    triangles.Add(new Triangle(
                        positions[indices[i]],
                        positions[indices[i + 1]],
                        positions[indices[i + 2]]
                    ));
                }
            }
        }
        return triangles;
    }

    // --- Build the BVH tree recursively ---
    static BVHNode BuildBVH(List<Triangle> tris, int depth = 0)
    {
        var node = new BVHNode();
        ComputeBounds(tris, out node.Min, out node.Max);

        const int leafSize = 8;
        const int maxDepth = 32;
        if (tris.Count <= leafSize || depth >= maxDepth)
        {
            node.Triangles = tris;
            return node;
        }

        // Choose the split axis (X/Y/Z) based on largest size
        Vector3 ext = node.Max - node.Min;
        int axis = 0;
        if (ext.Y > ext.X && ext.Y >= ext.Z) axis = 1;
        else if (ext.Z > ext.X && ext.Z >= ext.Y) axis = 2;

        float split = (axis == 0
            ? (node.Min.X + node.Max.X) * 0.5f
            : axis == 1
                ? (node.Min.Y + node.Max.Y) * 0.5f
                : (node.Min.Z + node.Max.Z) * 0.5f);

        var left = new List<Triangle>(tris.Count / 2);
        var right = new List<Triangle>(tris.Count / 2);

        // Split triangles based on centroid position
        for (int i = 0; i < tris.Count; i++)
        {
            var t = tris[i];
            var c = new Vector3(
                (t.A.X + t.B.X + t.C.X) / 3f,
                (t.A.Y + t.B.Y + t.C.Y) / 3f,
                (t.A.Z + t.B.Z + t.C.Z) / 3f
            );

            float cv = axis == 0 ? c.X : axis == 1 ? c.Y : c.Z;
            if (cv < split) left.Add(t);
            else right.Add(t);
        }

        // Avoid bad splits (if one side is empty)
        if (left.Count == 0 || right.Count == 0)
        {
            node.Triangles = tris;
            return node;
        }

        node.Left = BuildBVH(left, depth + 1);
        node.Right = BuildBVH(right, depth + 1);
        return node;
    }

    // --- Compute the min/max bounds of a triangle list ---
    static void ComputeBounds(List<Triangle> tris, out Vector3 min, out Vector3 max)
    {
        if (tris == null || tris.Count == 0)
        {
            min = max = Vector3.Zero;
            return;
        }

        min = new Vector3(float.PositiveInfinity);
        max = new Vector3(float.NegativeInfinity);

        for (int i = 0; i < tris.Count; i++)
        {
            var t = tris[i];
            ExpandBounds(ref min, ref max, t.A);
            ExpandBounds(ref min, ref max, t.B);
            ExpandBounds(ref min, ref max, t.C);
        }
    }

    // --- Expands bounding box to include a vertex ---
    static void ExpandBounds(ref Vector3 min, ref Vector3 max, Vector3 p)
    {
        if (p.X < min.X) min.X = p.X; if (p.Y < min.Y) min.Y = p.Y; if (p.Z < min.Z) min.Z = p.Z;
        if (p.X > max.X) max.X = p.X; if (p.Y > max.Y) max.Y = p.Y; if (p.Z > max.Z) max.Z = p.Z;
    }

    // --- Ray vs Bounding Box intersection test ---
    static bool RayIntersectsAABB(Vector3 origin, Vector3 dir, Vector3 min, Vector3 max, float maxDist)
    {
        const float EPS = 1e-8f;
        float tmin = 0f;
        float tmax = maxDist;

        // X axis
        if (Math.Abs(dir.X) < EPS)
        {
            if (origin.X < min.X || origin.X > max.X) return false;
        }
        else
        {
            float inv = 1f / dir.X;
            float t0 = (min.X - origin.X) * inv;
            float t1 = (max.X - origin.X) * inv;
            if (t0 > t1) (t0, t1) = (t1, t0);
            if (t0 > tmin) tmin = t0;
            if (t1 < tmax) tmax = t1;
            if (tmax <= tmin) return false;
        }

        // Y axis
        if (Math.Abs(dir.Y) < EPS)
        {
            if (origin.Y < min.Y || origin.Y > max.Y) return false;
        }
        else
        {
            float inv = 1f / dir.Y;
            float t0 = (min.Y - origin.Y) * inv;
            float t1 = (max.Y - origin.Y) * inv;
            if (t0 > t1) (t0, t1) = (t1, t0);
            if (t0 > tmin) tmin = t0;
            if (t1 < tmax) tmax = t1;
            if (tmax <= tmin) return false;
        }

        // Z axis
        if (Math.Abs(dir.Z) < EPS)
        {
            if (origin.Z < min.Z || origin.Z > max.Z) return false;
        }
        else
        {
            float inv = 1f / dir.Z;
            float t0 = (min.Z - origin.Z) * inv;
            float t1 = (max.Z - origin.Z) * inv;
            if (t0 > t1) (t0, t1) = (t1, t0);
            if (t0 > tmin) tmin = t0;
            if (t1 < tmax) tmax = t1;
            if (tmax <= tmin) return false;
        }

        return tmax > tmin && tmax >= 0f;
    }

    // --- Ray traversal through BVH nodes ---
    static bool RaycastAnyHit(BVHNode node, Vector3 origin, Vector3 dir, float maxDist)
    {
        if (node == null) return false;
        if (!RayIntersectsAABB(origin, dir, node.Min, node.Max, maxDist)) return false;

        if (node.IsLeaf)
        {
            foreach (var tri in node.Triangles)
            {
                if (RayIntersectsTriangle(origin, dir, tri, out float dist))
                {
                    if (dist > 0f && dist < maxDist) return true;
                }
            }
            return false;
        }

        // Recurse into both children
        if (RaycastAnyHit(node.Left, origin, dir, maxDist)) return true;
        if (RaycastAnyHit(node.Right, origin, dir, maxDist)) return true;
        return false;
    }

    // --- Möller–Trumbore ray/triangle intersection algorithm ---
    static bool RayIntersectsTriangle(Vector3 origin, Vector3 dir, Triangle tri, out float distance)
    {
        const float EPSILON = 1e-6f;
        distance = 0;

        Vector3 edge1 = tri.B - tri.A;
        Vector3 edge2 = tri.C - tri.A;

        Vector3 h = Vector3.Cross(dir, edge2);
        float a = Vector3.Dot(edge1, h);

        if (a > -EPSILON && a < EPSILON)
            return false; // ray is parallel to triangle

        float f = 1.0f / a;
        Vector3 s = origin - tri.A;
        float u = f * Vector3.Dot(s, h);
        if (u < 0.0 || u > 1.0) return false;

        Vector3 q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(dir, q);
        if (v < 0.0 || u + v > 1.0) return false;

        float t = f * Vector3.Dot(edge2, q);
        if (t > EPSILON)
        {
            distance = t;
            return true;
        }
        return false;
    }

    // --- Format vector for readable printing ---
    static string Vec(Vector3 v) => $"({v.X:F1}, {v.Y:F1}, {v.Z:F1})";
}
