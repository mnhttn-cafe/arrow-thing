using UnityEngine;

/// <summary>
/// Builds a polyline mesh for an arrow body with arc-length UVs.
///
/// Geometry: one rect quad per segment, one square fill quad per interior corner.
/// UV.x = cumulative arc length along path (for texture/pattern skinning).
/// UV.y = 0..1 across the width (used by the shader for the dome/bead profile).
///
/// A sliding window [windowStart, windowEnd] on the arc-length axis clips the
/// visible portion of the arrow. Defaults to the full path length.
/// </summary>
public static class ArrowMeshBuilder
{
    /// <summary>
    /// Builds a mesh from a world-space polyline.
    /// </summary>
    /// <param name="path">Ordered points along the arrow path (at least 2).</param>
    /// <param name="width">World-space width of the arrow body.</param>
    /// <param name="windowStart">Arc-length value at which the visible window begins.</param>
    /// <param name="windowEnd">Arc-length value at which the visible window ends. Pass
    /// <c>float.MaxValue</c> (or any value beyond total length) to show the full path.</param>
    /// <param name="headLength">Length of the arrowhead triangle beyond the last path point.
    /// Pass 0 to omit the arrowhead.</param>
    /// <param name="headWidthMultiplier">Half-base of the arrowhead as a multiple of body width.</param>
    public static Mesh Build(Vector3[]? path, float width, float windowStart = 0f, float windowEnd = float.MaxValue, float headLength = 0f, float headWidthMultiplier = 1.2f)
    {
        if (path == null || path.Length < 2)
        {
            Debug.LogWarning("ArrowMeshBuilder.Build: path must have at least 2 points.");
            return new Mesh();
        }

        float half = width * 0.5f;

        // Pre-compute cumulative arc lengths so we can assign UV.x correctly.
        float[] arcLength = ComputeArcLengths(path);
        float totalLength = arcLength[arcLength.Length - 1];
        float wEnd = Mathf.Min(windowEnd, totalLength);

        var vertices = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var triangles = new System.Collections.Generic.List<int>();

        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector3 a = path[i];
            Vector3 b = path[i + 1];
            float uA = arcLength[i];
            float uB = arcLength[i + 1];

            // Skip segments entirely outside the window.
            if (uB < windowStart || uA > wEnd) continue;

            // Clamp segment endpoints to the window.
            if (uA < windowStart)
            {
                float t = (windowStart - uA) / (uB - uA);
                a = Vector3.Lerp(a, b, t);
                uA = windowStart;
            }
            if (uB > wEnd)
            {
                float t = (wEnd - uA) / (uB - uA);
                b = Vector3.Lerp(a, b, t);
                uB = wEnd;
            }

            Vector3 dir = (b - a).normalized;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f) * half;

            AddQuad(vertices, uvs, triangles,
                a - perp, a + perp, b - perp, b + perp,
                uA, uB);

            // Fill corner gap between this segment and the next.
            if (i < path.Length - 2 && arcLength[i + 1] >= windowStart && arcLength[i + 1] <= wEnd)
            {
                Vector3 c = path[i + 2];
                Vector3 dir2 = (c - b).normalized;
                Vector3 perp2 = new Vector3(-dir2.y, dir2.x, 0f) * half;

                // Square fill: spans from b-perp / b+perp to b-perp2 / b+perp2.
                // All 4 corners share the same arc-length value (the corner point).
                AddQuad(vertices, uvs, triangles,
                    b - perp, b + perp, b - perp2, b + perp2,
                    uB, uB);
            }
        }

        // Arrowhead triangle at the head end of the path (path[0] is the head).
        if (headLength > 0f && path.Length >= 2 && arcLength[0] >= windowStart && arcLength[0] <= wEnd)
        {
            Vector3 headPos = path[0];
            Vector3 headDir = (path[0] - path[1]).normalized;
            Vector3 headPerp = new Vector3(-headDir.y, headDir.x, 0f);

            // Base of the triangle is wider than the body
            float headHalfBase = width * headWidthMultiplier;
            Vector3 baseLeft = headPos - headPerp * headHalfBase;
            Vector3 baseRight = headPos + headPerp * headHalfBase;
            Vector3 tip = headPos + headDir * headLength;

            float uHead = arcLength[0];

            int baseIdx = vertices.Count;
            vertices.Add(baseLeft); uvs.Add(new Vector2(uHead, 0f));
            vertices.Add(baseRight); uvs.Add(new Vector2(uHead, 1f));
            vertices.Add(tip); uvs.Add(new Vector2(uHead, 0.5f));

            triangles.Add(baseIdx + 0);
            triangles.Add(baseIdx + 1);
            triangles.Add(baseIdx + 2);
        }

        var mesh = new Mesh
        {
            name = "ArrowBody"
        };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // -------------------------------------------------------------------------

    private static float[] ComputeArcLengths(Vector3[] path)
    {
        var lengths = new float[path.Length];
        lengths[0] = 0f;
        for (int i = 1; i < path.Length; i++)
            lengths[i] = lengths[i - 1] + Vector3.Distance(path[i - 1], path[i]);
        return lengths;
    }

    /// <summary>
    /// Appends two triangles (a quad) to the lists.
    /// Layout: v0=bottomLeft, v1=topLeft, v2=bottomRight, v3=topRight (in perp terms).
    /// </summary>
    private static void AddQuad(
        System.Collections.Generic.List<Vector3> verts,
        System.Collections.Generic.List<Vector2> uvs,
        System.Collections.Generic.List<int> tris,
        Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
        float uStart, float uEnd)
    {
        int base_ = verts.Count;

        verts.Add(v0); uvs.Add(new Vector2(uStart, 0f));
        verts.Add(v1); uvs.Add(new Vector2(uStart, 1f));
        verts.Add(v2); uvs.Add(new Vector2(uEnd, 0f));
        verts.Add(v3); uvs.Add(new Vector2(uEnd, 1f));

        // Two counter-clockwise triangles (Unity winding).
        tris.Add(base_ + 0); tris.Add(base_ + 1); tris.Add(base_ + 2);
        tris.Add(base_ + 2); tris.Add(base_ + 1); tris.Add(base_ + 3);
    }
}
